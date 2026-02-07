use serde::Serialize;
use serde_json::Value;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum NativeCoreError {
    #[error("invalid utf-8 input: {0}")]
    Utf8(String),
    #[error("invalid json payload: {0}")]
    Json(String),
}

#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
pub struct ParsedKeyframeData {
    pub total_duration_ticks: i64,
    pub keyframe_ticks: Vec<i64>,
}

pub fn normalize_ffprobe_json(input: &[u8]) -> Result<Vec<u8>, NativeCoreError> {
    let mut payload: Value = serde_json::from_slice(input)
        .map_err(|err| NativeCoreError::Json(err.to_string()))?;

    let streams = payload
        .get_mut("streams")
        .and_then(Value::as_array_mut)
        .into_iter()
        .flatten();

    for stream in streams {
        normalize_aspect_field(stream, "display_aspect_ratio");
        normalize_aspect_field(stream, "sample_aspect_ratio");
    }

    serde_json::to_vec(&payload).map_err(|err| NativeCoreError::Json(err.to_string()))
}

pub fn parse_ffprobe_keyframe_csv(input: &[u8]) -> Result<ParsedKeyframeData, NativeCoreError> {
    let csv = std::str::from_utf8(input)
        .map_err(|err| NativeCoreError::Utf8(err.to_string()))?;

    let mut keyframes = Vec::new();
    let mut stream_duration = 0f64;
    let mut format_duration = 0f64;

    for line in csv.lines() {
        let line = line.trim();
        if line.is_empty() {
            continue;
        }

        let mut first_split = line.splitn(2, ',');
        let line_type = first_split.next().unwrap_or_default();
        let rest = first_split.next().unwrap_or_default();

        match line_type {
            "packet" => {
                let mut packet_split = rest.splitn(2, ',');
                let pts_time = packet_split.next().unwrap_or_default();
                let flags = packet_split.next().unwrap_or_default();

                if flags.starts_with("K_") {
                    if let Ok(keyframe_secs) = pts_time.parse::<f64>() {
                        keyframes.push((keyframe_secs * 10_000_000f64).round() as i64);
                    }
                }
            }
            "stream" => {
                if let Ok(duration) = rest.parse::<f64>() {
                    stream_duration = duration;
                }
            }
            "format" => {
                if let Ok(duration) = rest.parse::<f64>() {
                    format_duration = duration;
                }
            }
            _ => {}
        }
    }

    let duration = if stream_duration > 0f64 {
        stream_duration
    } else {
        format_duration
    };

    Ok(ParsedKeyframeData {
        total_duration_ticks: (duration * 10_000_000f64).round() as i64,
        keyframe_ticks: keyframes,
    })
}

fn normalize_aspect_field(stream: &mut Value, field: &str) {
    let Some(value) = stream.get_mut(field) else {
        return;
    };

    if value
        .as_str()
        .map(|v| v.eq_ignore_ascii_case("0:1"))
        .unwrap_or(false)
    {
        *value = Value::String(String::new());
    }
}
