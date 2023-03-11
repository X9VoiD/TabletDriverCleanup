use std::collections::hash_map::Entry;
use std::collections::HashMap;
use std::sync::Mutex;

use lazy_static::lazy_static;
use regex::{Regex, RegexBuilder};

lazy_static! {
    static ref REGEX_CACHE: Mutex<HashMap<String, Regex>> = Mutex::new(HashMap::new());
}

pub fn cached_match(input: Option<&str>, regex_pattern: Option<&str>) -> bool {
    let regex_pattern = match regex_pattern {
        Some(regex_pattern) => regex_pattern,
        None => return true,
    };

    let input = match input {
        Some(input) => input,
        None => return false,
    };

    let mut cache = REGEX_CACHE.lock().unwrap();
    let regex = {
        match cache.get(regex_pattern) {
            Some(regex) => regex,
            None => {
                let regex = build_regex(regex_pattern);
                let Entry::Vacant(vacant) = cache.entry(regex_pattern.to_string()) else { unreachable!() };
                vacant.insert(regex)
            }
        }
    };

    regex.is_match(input)
}

fn build_regex(regex: &str) -> Regex {
    RegexBuilder::new(regex)
        .case_insensitive(true)
        .build()
        .unwrap()
}
