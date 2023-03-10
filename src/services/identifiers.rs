use std::{
    fs::File,
    io::{Read, Write},
    path::Path,
};

use crate::{no_color, State};

use error_stack::{bail, report, IntoReport, Result, ResultExt};
use include_dir::include_dir;
use log::{error, info, warn};
use thiserror::Error;

static EMBEDDED_IDENTIFIERS: include_dir::Dir = include_dir!("$CARGO_MANIFEST_DIR/config");

pub enum Source {
    Embed(&'static [u8]),
    Local(Vec<u8>),
    Remote(Vec<u8>),
}

impl Source {
    pub fn get_content(&self) -> &[u8] {
        match self {
            Source::Embed(content) => content,
            Source::Local(resource) => resource,
            Source::Remote(resource) => resource,
        }
    }
}

#[derive(Debug, Error)]
pub enum RetrievalErr {
    #[error("Retrieval Error: Getting a resource from '{0}' is not allowed")]
    Disallowed(&'static str),
    #[error("Retrieval Error: Failed to get resource {0} {1}")]
    Err(&'static str, RetrievalMethod),
}

#[derive(Debug)]
pub enum RetrievalMethod {
    Offline,
    Online,
    Embedded,
}

impl std::fmt::Display for RetrievalMethod {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            RetrievalMethod::Offline => write!(f, "offline"),
            RetrievalMethod::Online => write!(f, "online"),
            RetrievalMethod::Embedded => write!(f, "embedded"),
        }
    }
}

pub async fn get_resource(identifier: &'static str, state: &State) -> Result<Source, RetrievalErr> {
    let resource = get_resource_offline(identifier, state);

    match resource {
        Ok(resource) => {
            no_color(|| info!("Got resource '{} offline'", identifier));
            return Ok(resource);
        }
        Err(err) => match err.current_context() {
            RetrievalErr::Disallowed(_) => {}
            _ => no_color(|| warn!("{:?}", err)),
        },
    }

    let resource = get_resource_online(identifier, state)
        .await
        .attach_printable_lazy(|| format!("cannot get '{}' online", identifier));

    match resource {
        Ok(resource) => {
            no_color(|| info!("Got resource '{}' online", identifier));
            return Ok(resource);
        }
        Err(err) => match err.current_context() {
            RetrievalErr::Disallowed(_) => {}
            _ => no_color(|| warn!("{:?}", err)),
        },
    }

    let resource = get_resource_embed(identifier, state)
        .attach_printable_lazy(|| format!("cannot get '{}' embedded", identifier));

    match resource {
        Ok(resource) => {
            no_color(|| info!("Got resource '{}' embedded", identifier));
            return Ok(resource);
        }
        Err(err) => {
            no_color(|| warn!("{:?}", err));
            return Err(err);
        }
    }
}

fn get_resource_offline(identifier: &'static str, state: &State) -> Result<Source, RetrievalErr> {
    if !state.use_cache {
        bail!(RetrievalErr::Disallowed("offline"));
    }

    let path = &Path::new(&state.current_path).join("config");
    if !path.exists() {
        std::fs::create_dir_all(path).unwrap();
        return Err(report!(RetrievalErr::Err(
            identifier,
            RetrievalMethod::Offline
        )))
        .attach_printable_lazy(|| format!("path {:?} does not exist", path));
    }

    let path = path.join(identifier);
    let mut file = File::open(&path)
        .into_report()
        .change_context(RetrievalErr::Err(identifier, RetrievalMethod::Offline))
        .attach_printable_lazy(|| format!("cannot open a handle to {:?}", path))?;

    let mut content = Vec::new();
    file.read_to_end(&mut content)
        .into_report()
        .change_context(RetrievalErr::Err(identifier, RetrievalMethod::Offline))
        .attach_printable_lazy(|| format!("cannot read from {:?}", path))?;

    Ok(Source::Local(content))
}

async fn get_resource_online(
    identifier: &'static str,
    state: &State,
) -> Result<Source, RetrievalErr> {
    if !state.allow_updates {
        bail!(RetrievalErr::Disallowed("online"))
    }

    let base_url = "https://raw.githubusercontent.com/X9VoiD/TabletDriverCleanup";
    let git_ref = "v4.x";
    let url = format!("{base_url}/{git_ref}/config/{identifier}");

    let response = reqwest::get(&url)
        .await
        .into_report()
        .change_context(RetrievalErr::Err(identifier, RetrievalMethod::Online))
        .attach_printable_lazy(|| format!("cannot get resource from {url}"))?;

    if !response.status().is_success() {
        return Err(report!(RetrievalErr::Err(
            identifier,
            RetrievalMethod::Online
        )))
        .attach_printable_lazy(|| {
            format!(
                "response status code is not success: {:?}",
                response.status()
            )
        });
    }

    let content = response
        .bytes()
        .await
        .into_report()
        .change_context(RetrievalErr::Err(identifier, RetrievalMethod::Online))
        .attach_printable_lazy(|| format!("cannot get resource content from {url}"))?
        .to_vec();

    if state.use_cache {
        let path = Path::new(&state.current_path)
            .join("config")
            .join(identifier);
        let mut file = File::create(&path)
            .into_report()
            .change_context(RetrievalErr::Err(identifier, RetrievalMethod::Online))
            .attach_printable_lazy(|| format!("cannot create a handle to {:?}", path))?;
        file.write_all(&content).unwrap();
    }

    Ok(Source::Remote(content))
}

fn get_resource_embed(identifier: &'static str, _state: &State) -> Result<Source, RetrievalErr> {
    Ok(Source::Embed(
        match EMBEDDED_IDENTIFIERS.get_file(identifier) {
            Some(file) => file.contents(),
            None => {
                return Err(report!(RetrievalErr::Err(
                    identifier,
                    RetrievalMethod::Embedded
                )))
                .attach_printable_lazy(|| {
                    format!("embedded resource '{identifier}' does not exist")
                })
            }
        },
    ))
}
