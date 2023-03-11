use std::io::{stdout, Write};
use std::time::Duration;

use crossterm::cursor;
use crossterm::event::{poll, read, Event, KeyCode, KeyEvent, KeyEventKind};
use crossterm::execute;
use crossterm::terminal;
use error_stack::{IntoReport, Result, ResultExt};
use thiserror::Error;
use tokio::time::sleep;
use tokio_util::sync::CancellationToken;

#[derive(PartialEq)]
pub enum PromptResult {
    Yes,
    No,
    Cancel,
}

#[derive(Debug, Error)]
#[error("Failed to read key")]
pub struct ReadKeyError {}

pub struct TempPrintGuard {
    pos_x: u16,
    pos_y: u16,
}

impl Drop for TempPrintGuard {
    fn drop(&mut self) {
        execute!(
            stdout(),
            cursor::MoveTo(self.pos_x, self.pos_y),
            terminal::Clear(terminal::ClearType::FromCursorDown),
        )
        .unwrap();
    }
}

pub fn enter_temp_print() -> TempPrintGuard {
    let (pos_x, pos_y) = cursor::position().unwrap();

    TempPrintGuard { pos_x, pos_y }
}

pub fn prompt_yes_no(message: &str) -> PromptResult {
    let get_key = || {
        temporary_print(|| {
            print!("{} (Y/n) ", message);
            std::io::stdout().flush().unwrap();
            read_key().unwrap()
        })
    };

    loop {
        match get_key().code {
            KeyCode::Char('y') | KeyCode::Enter => break PromptResult::Yes,
            KeyCode::Char('n') => break PromptResult::No,
            KeyCode::Esc => break PromptResult::Cancel,
            _ => {}
        }
    }
}

pub fn temporary_print<T>(action: impl FnOnce() -> T) -> T {
    let _guard = enter_temp_print();
    action()
}

pub fn read_key() -> Result<KeyEvent, ReadKeyError> {
    loop {
        if let Event::Key(key) = read().into_report().change_context(ReadKeyError {})? {
            if key.kind == KeyEventKind::Press {
                break Ok(key);
            }
        }
    }
}

pub async fn read_key_async(ct: Option<CancellationToken>) -> Result<WaitResult, ReadKeyError> {
    tokio::spawn(async move {
        loop {
            if ct.is_some() && ct.as_ref().unwrap().is_cancelled() {
                return Ok(WaitResult::Cancelled);
            }
            if poll(Duration::from_secs(0))
                .into_report()
                .change_context(ReadKeyError {})?
            {
                if let Event::Key(event) = read().into_report().change_context(ReadKeyError {})? {
                    if event.kind == KeyEventKind::Press {
                        break Ok(WaitResult::Key(event));
                    }
                }
            } else {
                sleep(Duration::from_millis(20)).await;
            }
        }
    })
    .await
    .unwrap()
}

#[derive(Debug)]
pub enum WaitResult {
    Key(KeyEvent),
    Cancelled,
}
