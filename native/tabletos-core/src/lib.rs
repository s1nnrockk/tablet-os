use std::ffi::CStr;
use std::os::raw::c_char;

/// Unicode-aware, allocation-light app-label matcher.
/// Returns 1 for a match and 0 for no match or invalid input.
#[no_mangle]
pub unsafe extern "C" fn tabletos_match(query: *const c_char, label: *const c_char) -> u8 {
    if query.is_null() || label.is_null() { return 0; }
    let query = match CStr::from_ptr(query).to_str() { Ok(v) => v, Err(_) => return 0 };
    let label = match CStr::from_ptr(label).to_str() { Ok(v) => v, Err(_) => return 0 };
    if query.is_empty() { return 1; }
    let mut wanted_iter = query.chars().flat_map(char::to_lowercase);
    let mut wanted = match wanted_iter.next() { Some(c) => c, None => return 1 };
    for candidate in label.chars().flat_map(char::to_lowercase) {
        if candidate == wanted {
            match wanted_iter.next() {
                Some(next) => wanted = next,
                None => return 1,
            }
        }
    }
    0
}
