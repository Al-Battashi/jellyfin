use jf_native_core::{normalize_ffprobe_json, parse_ffprobe_keyframe_csv};
use std::ptr;

#[repr(C)]
pub struct JfNativeBuffer {
    pub ptr: *mut u8,
    pub len: usize,
}

#[no_mangle]
pub extern "C" fn jf_native_healthcheck() -> i32 {
    1
}

#[no_mangle]
pub extern "C" fn jf_native_free_buffer(ptr: *mut u8, len: usize) {
    if ptr.is_null() || len == 0 {
        return;
    }

    unsafe {
        let _ = Vec::from_raw_parts(ptr, len, len);
    }
}

#[no_mangle]
pub extern "C" fn jf_native_normalize_ffprobe_json(
    input_ptr: *const u8,
    input_len: usize,
    output: *mut JfNativeBuffer,
    error: *mut JfNativeBuffer,
) -> i32 {
    run_with_buffers(input_ptr, input_len, output, error, normalize_ffprobe_json)
}

#[no_mangle]
pub extern "C" fn jf_native_parse_keyframe_csv(
    input_ptr: *const u8,
    input_len: usize,
    output: *mut JfNativeBuffer,
    error: *mut JfNativeBuffer,
) -> i32 {
    run_with_buffers(input_ptr, input_len, output, error, |input| {
        parse_ffprobe_keyframe_csv(input)
            .map_err(|e| e.to_string())
            .and_then(|parsed| serde_json::to_vec(&parsed).map_err(|e| e.to_string()))
    })
}

fn run_with_buffers<F, E>(
    input_ptr: *const u8,
    input_len: usize,
    output: *mut JfNativeBuffer,
    error: *mut JfNativeBuffer,
    action: F,
) -> i32
where
    F: Fn(&[u8]) -> Result<Vec<u8>, E>,
    E: std::fmt::Display,
{
    if output.is_null() || error.is_null() {
        return -1;
    }

    unsafe {
        ptr::write(
            output,
            JfNativeBuffer {
                ptr: ptr::null_mut(),
                len: 0,
            },
        );

        ptr::write(
            error,
            JfNativeBuffer {
                ptr: ptr::null_mut(),
                len: 0,
            },
        );
    }

    if input_ptr.is_null() || input_len == 0 {
        write_error(error, "input cannot be empty");
        return -1;
    }

    let input = unsafe { std::slice::from_raw_parts(input_ptr, input_len) };

    match action(input) {
        Ok(bytes) => {
            write_buffer(output, bytes);
            0
        }
        Err(err) => {
            write_error(error, &err.to_string());
            -1
        }
    }
}

fn write_error(error: *mut JfNativeBuffer, value: &str) {
    write_buffer(error, value.as_bytes().to_vec());
}

fn write_buffer(target: *mut JfNativeBuffer, mut bytes: Vec<u8>) {
    if bytes.is_empty() {
        unsafe {
            ptr::write(
                target,
                JfNativeBuffer {
                    ptr: ptr::null_mut(),
                    len: 0,
                },
            );
        }

        return;
    }

    bytes.shrink_to_fit();
    let ptr = bytes.as_mut_ptr();
    let len = bytes.len();
    std::mem::forget(bytes);

    unsafe {
        ptr::write(target, JfNativeBuffer { ptr, len });
    }
}
