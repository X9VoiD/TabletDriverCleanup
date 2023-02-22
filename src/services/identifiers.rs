use std::{
    fs::File,
    io::{Read, Write},
    path::Path,
};

use crate::State;

use error_stack::{bail, report, IntoReport, Result, ResultExt};
use include_dir::include_dir;
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
    #[error("getting a resource from '{0}' is not allowed")]
    Disallowed(&'static str),
    #[error("failed to get resource")]
    Err,
}

pub async fn get_resource(identifier: &str, state: &State) -> Result<Source, RetrievalErr> {
    let resource = get_resource_offline(identifier, state);
    if resource.is_ok() {
        return resource;
    }

    let resource = get_resource_online(identifier, state).await;
    if resource.is_ok() {
        return resource;
    }

    get_resource_embed(identifier, state)
}

fn get_resource_offline(identifier: &str, state: &State) -> Result<Source, RetrievalErr> {
    if !state.use_cache {
        bail!(RetrievalErr::Disallowed("offline"));
    }

    let path = &Path::new(&state.current_path)
        .join("config")
        .join(identifier);
    if !path.exists() {
        return Err(report!(RetrievalErr::Err))
            .attach_printable_lazy(|| format!("path '{:?}' does not exist", path));
    }

    let mut file = File::open(path)
        .into_report()
        .change_context(RetrievalErr::Err)
        .attach_printable_lazy(|| format!("cannot open a handle to '{:?}'", path))?;

    let mut content = Vec::new();
    file.read_to_end(&mut content)
        .into_report()
        .change_context(RetrievalErr::Err)
        .attach_printable_lazy(|| format!("cannot read from '{:?}'", path))?;

    Ok(Source::Local(content))
}

async fn get_resource_online(identifier: &str, state: &State) -> Result<Source, RetrievalErr> {
    if !state.allow_updates {
        bail!(RetrievalErr::Disallowed("online"))
    }

    let base_url = "https://raw.githubusercontent.com/X9VoiD/TabletDriverCleanup";
    let git_ref = "v4.x";
    let url = format!("{base_url}/{git_ref}/config/{identifier}");

    let response = reqwest::get(&url)
        .await
        .into_report()
        .change_context(RetrievalErr::Err)
        .attach_printable_lazy(|| format!("cannot get resource from '{url}'"))?;

    if !response.status().is_success() {
        return Err(report!(RetrievalErr::Err)).attach_printable_lazy(|| {
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
        .change_context(RetrievalErr::Err)
        .attach_printable_lazy(|| format!("cannot get resource content from '{url}'"))?
        .to_vec();

    if state.use_cache {
        let path = Path::new(&state.current_path)
            .join("config")
            .join(identifier);
        let mut file = File::create(&path)
            .into_report()
            .change_context(RetrievalErr::Err)
            .attach_printable_lazy(|| format!("cannot create a handle to '{:?}'", path))?;
        file.write_all(&content).unwrap();
    }

    Ok(Source::Remote(content))
}

fn get_resource_embed(identifier: &str, _state: &State) -> Result<Source, RetrievalErr> {
    Ok(Source::Embed(
        match EMBEDDED_IDENTIFIERS.get_file(identifier) {
            Some(file) => file.contents(),
            None => {
                return Err(report!(RetrievalErr::Err)).attach_printable_lazy(|| {
                    format!("embedded resource '{identifier}' does not exist")
                })
            }
        },
    ))
}
