use clap::{command, Arg, ArgAction, Command};
use tabletdrivercleanup::{cleanup_modules::*, *};

use simplelog::{self, WriteLogger};

#[tokio::main]
async fn main() {
    WriteLogger::init(
        simplelog::LevelFilter::Info,
        simplelog::Config::default(),
        std::fs::File::create("log.txt").unwrap(),
    )
    .unwrap();

    let modules: Vec<Box<dyn Module>> = vec![
        Box::new(DriverPackageCleanupModule::new()),
        Box::new(DeviceCleanupModule::new()),
        Box::new(DriverCleanupModule::new()),
    ];

    let command = command!()
        .arg(
            Arg::new(constants::DRY_RUN)
                .long("dry-run")
                .short('d')
                .help("Only print what would be done, do not actually do anything")
                .action(ArgAction::SetTrue)
                .required(false),
        )
        .arg(
            Arg::new(constants::DUMP)
                .long("dump")
                .short('D')
                .help("Dump information about the system")
                .action(ArgAction::SetTrue)
                .required(false),
        )
        .arg(
            Arg::new(constants::INTERACTIVE)
                .long("no-prompt")
                .short('s')
                .help("Do not prompt for user input. Useful for scripting")
                .action(ArgAction::SetFalse)
                .required(false),
        )
        .arg(
            Arg::new(constants::USE_CACHE)
                .long("no-cache")
                .short('c')
                .help("Do not use cached identifiers")
                .action(ArgAction::SetFalse)
                .required(false),
        )
        .arg(
            Arg::new(constants::ALLOW_UPDATES)
                .long("no-update")
                .short('u')
                .help("Do not check online for identifier updates")
                .action(ArgAction::SetFalse)
                .required(false),
        );

    let matches = add_modules_to_command(command, &modules).get_matches();
    let mode = match matches.get_flag("dump") {
        true => Mode::Dump,
        false => Mode::Run,
    };

    let config = tabletdrivercleanup::parse_to_config(modules, matches);

    match mode {
        Mode::Run => tabletdrivercleanup::run(config).await,
        Mode::Dump => tabletdrivercleanup::dump(config).await,
    };
}

fn add_modules_to_command(mut command: Command, modules: &[Box<dyn Module>]) -> Command {
    for module in modules {
        command = configure_command(module.as_ref(), command);
    }
    command
}

fn configure_command(module: &dyn Module, command: Command) -> Command {
    command.arg(
        Arg::new(module.cli_name().to_string())
            .long(format!("no-{}", module.cli_name()))
            .action(ArgAction::SetFalse)
            .help(format!("Do not {}", module.help())),
    )
}
