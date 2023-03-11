pub mod cleanup_modules;
pub(crate) mod services;

use std::path::PathBuf;

use clap::ArgMatches;
use cleanup_modules::Module;
use crossterm::event::KeyCode;
use crossterm::style::Stylize;
use error_stack::fmt::ColorMode;
use error_stack::Report;

use crate::services::terminal::{read_key_async, WaitResult};

pub mod constants {
    pub const CLI_NAME: &str = "TabletDriverCleanup";
    pub const DRY_RUN: &str = "dry_run";
    pub const DUMP: &str = "dump";
    pub const INTERACTIVE: &str = "interactive";
    pub const USE_CACHE: &str = "use_cache";
    pub const ALLOW_UPDATES: &str = "allow_updates";
}

pub type ModuleCollection = Vec<Box<dyn Module>>;

#[derive(Debug)]
pub enum Mode {
    Run,
    Dump,
}

#[derive(Default)]
pub struct Config {
    pub state: State,
    pub modules: ModuleCollection,
}

#[derive(Default)]
pub struct State {
    pub current_path: PathBuf,
    pub interactive: bool,
    pub dry_run: bool,
    pub use_cache: bool,
    pub allow_updates: bool,
}

#[derive(Default)]
pub struct ConfigBuilder {
    config: Config,
}

impl ConfigBuilder {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn current_path(mut self, path: PathBuf) -> Self {
        self.config.state.current_path = path;
        self
    }

    pub fn interactive(mut self, interactive: bool) -> Self {
        self.config.state.interactive = interactive;
        self
    }

    pub fn dry_run(mut self, dry_run: bool) -> Self {
        self.config.state.dry_run = dry_run;
        self
    }

    pub fn use_cache(mut self, use_cache: bool) -> Self {
        self.config.state.use_cache = use_cache;
        self
    }

    pub fn allow_updates(mut self, allow_updates: bool) -> Self {
        self.config.state.allow_updates = allow_updates;
        self
    }

    pub fn add_module(mut self, module: Box<dyn Module>) -> Self {
        self.config.modules.push(module);
        self
    }

    pub fn build(self) -> Config {
        self.config
    }
}

#[derive(Default)]
struct RunState {
    pub need_reboot: bool,
}

pub async fn run(config: Config) {
    print_header();
    let state = config.state;
    let mut modules = config.modules;

    if !state.dry_run && !services::windows::process_is_elevated() {
        eprintln!("This program must be run as administrator.");
        if state.interactive {
            println!("Press any key to exit...");
            _ = read_key_async(None).await;
            std::process::exit(1);
        }
    }

    let mut run_state: RunState = Default::default();

    if state.dry_run {
        println!("Running in dry run mode. No changes will be made.");
    }

    for module in modules.iter_mut() {
        println!("\nRunning '{}'...", module.name());

        match module.run(&state).await {
            Err(error) => {
                eprintln!("\n{}", "Error!".red());
                eprintln!("{:?}", error);
                eprintln!(
                    "\nErrors were encountered while running '{}'. Aborting!",
                    module.name()
                );

                if state.interactive {
                    println!("Press any key to exit...");
                    _ = read_key_async(None).await;
                }

                std::process::exit(1);
            }
            Ok(module_run) if module_run.reboot_required => {
                run_state.need_reboot = true;
            }
            Ok(_) => {}
        }
    }

    if run_state.need_reboot {
        if state.interactive {
            println!("\nReboot is required to complete the cleanup.");
            println!("Press any key to reboot now, or press 'q' to cancel reboot... ");

            if let WaitResult::Key(key) = read_key_async(None).await.unwrap() {
                if key.code == KeyCode::Char('q') {
                    println!("Reboot cancelled.");
                    return;
                }
            }

            std::process::Command::new("shutdown")
                .arg("/r")
                .arg("/t")
                .arg("0")
                .spawn()
                .expect("Failed to execute shutdown command.");
        }

        return;
    }

    if state.interactive {
        println!("\nCleanup complete. Press any key to exit... ");
        _ = read_key_async(None).await;
    }
}

pub async fn dump(config: Config) {
    print_header();
    println!("\nDumping into {}...", config.state.current_path.display());

    let (state, modules) = (config.state, config.modules);

    for module in modules.iter() {
        let dumper = match module.get_dumper() {
            Some(dumper) => dumper,
            None => continue,
        };

        let result = dumper.dump(&state).await;
        if let Err(err) = result {
            eprintln!("{:?}", err);
            eprintln!()
        }
    }
}

fn print_header() {
    println!("TabletDriverCleanup v{}", env!("CARGO_PKG_VERSION"));
}

pub fn parse_to_config(modules: Vec<Box<dyn Module>>, matches: ArgMatches) -> Config {
    let mut current_path: PathBuf = std::env::args().next().unwrap().into();
    current_path.pop();

    let mut builder = ConfigBuilder::new()
        .current_path(current_path)
        .dry_run(matches.get_flag(constants::DRY_RUN))
        .interactive(matches.get_flag(constants::INTERACTIVE))
        .use_cache(matches.get_flag(constants::USE_CACHE))
        .allow_updates(matches.get_flag(constants::ALLOW_UPDATES));

    for module in modules {
        let name = module.cli_name();
        if matches.get_flag(name) {
            builder = builder.add_module(module);
        }
    }

    builder.build()
}

fn no_color(action: impl FnOnce()) {
    Report::set_color_mode(ColorMode::None);
    action();
    Report::set_color_mode(ColorMode::default());
}
