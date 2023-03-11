use std::collections::HashMap;

use lazy_static::lazy_static;
use regex::{Regex, RegexBuilder};

lazy_static! {
    static ref INTEREST_CACHE: HashMap::<&'static str, Regex> = {
        create_map(&[
            "10moon",
            "Acepen",
            "Artisul",
            "Digitizer",
            "EMR",
            "filtr",
            "Gaomon",
            "Genius",
            "Huion",
            "Kenting",
            "libwdi",
            "Lifetec",
            "Monoprice",
            "Parblo",
            "RobotPen",
            "Tablet",
            "UC[-| ]?Logic",
            "UGEE",
            "Veikk",
            "ViewSonic",
            r"v\w*hid",
            "Wacom",
            "WinUSB",
            "XenceLabs",
            "XENX",
            "XP[-| ]?Pen",
        ])
    };
    static ref COUNTER_INTEREST_CACHE: HashMap::<&'static str, Regex> =
        create_map(&["android", "logitech",]);
}

pub fn is_of_interest(string: Option<&str>) -> bool {
    let string = match string {
        Some(string) => string,
        None => return false,
    };

    for regex in INTEREST_CACHE.values() {
        if regex.is_match(string) {
            for regex in COUNTER_INTEREST_CACHE.values() {
                if regex.is_match(string) {
                    return false;
                }
            }
            return true;
        }
    }

    false
}

pub fn is_of_interest_iter<'a>(mut strings: impl Iterator<Item = &'a str>) -> bool {
    strings.any(|string| is_of_interest(Some(string)))
}

fn create_map(interests: &[&'static str]) -> HashMap<&'static str, Regex> {
    let mut map = HashMap::new();
    for interest in interests {
        add_interest(&mut map, interest);
    }

    map
}

fn add_interest(map: &mut HashMap<&'static str, Regex>, string: &'static str) {
    map.insert(
        string,
        RegexBuilder::new(string)
            .case_insensitive(true)
            .build()
            .unwrap(),
    );
}
