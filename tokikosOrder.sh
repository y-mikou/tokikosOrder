#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C

default_output='./tokikosOrder_output.jpg'

usage() {
    cat >&2 <<'EOF'
使い方: tokikosOrder.sh <表紙高さmm> <表紙幅mm> <背幅mm> <断ち切りmm> <塗り足しmm> <350|600> [出力.jpg]
例: tokikosOrder.sh 210 148 12 3 3 350 ./cover.jpg
EOF
}

if (($# < 6 || $# > 7)); then
    echo 'エラー: 引数は6個（出力先を指定する場合は7個）必要です。' >&2
    usage
    exit 1
fi

cover_height=$1
cover_width=$2
spine_width=$3
trim=$4
bleed=$5
dpi=$6
if (($# == 7)); then
    output_path=$7
else
    output_path=$default_output
fi

is_number() {
    local value=$1 requirement=$2
    awk -v value="$value" -v requirement="$requirement" 'BEGIN {
        if (value !~ /^([0-9]+([.][0-9]*)?|[.][0-9]+)$/) exit 1
        n = value + 0
        if (requirement == "positive" && n <= 0) exit 1
        if (requirement == "nonnegative" && n < 0) exit 1
    }'
}

for spec in "表紙サイズ・縦:$cover_height:positive" "表紙サイズ・横:$cover_width:positive" \
            "背幅:$spine_width:nonnegative" "断ち切り幅:$trim:nonnegative" \
            "塗り足し幅:$bleed:nonnegative"; do
    IFS=: read -r label value requirement <<<"$spec"
    if ! is_number "$value" "$requirement"; then
        if [[ $requirement == positive ]]; then
            echo "エラー: $label は0より大きい数値（mm）で指定してください: $value" >&2
        else
            echo "エラー: $label は0以上の数値（mm）で指定してください: $value" >&2
        fi
        exit 1
    fi
done

if [[ $dpi != 350 && $dpi != 600 ]]; then
    echo 'エラー: 解像度は350または600を指定してください。' >&2
    exit 1
fi
if ! awk -v h="$cover_height" -v w="$cover_width" -v b="$bleed" \
    'BEGIN { exit !(2*b < h && 2*b < w) }'; then
    echo 'エラー: 塗り足し幅の2倍は、表紙の高さと幅より小さくしてください。' >&2
    exit 1
fi
if [[ -z $output_path ]]; then
    echo 'エラー: 出力先パスを空にすることはできません。' >&2
    exit 1
fi

# Convert a millimeter boundary to the nearest pixel, rounding half up.
mm_to_px() {
    awk -v mm="$1" -v dpi="$dpi" 'BEGIN {
        pixels = mm * dpi / 25.4
        if (pixels < 0 || pixels > 2147483000) exit 1
        printf "%.0f", int(pixels + 0.5)
    }'
}

total_width_mm=$(awk -v w="$cover_width" -v s="$spine_width" -v t="$trim" \
    'BEGIN { printf "%.12f", 2*w + s + 2*t }')
total_height_mm=$(awk -v h="$cover_height" -v t="$trim" \
    'BEGIN { printf "%.12f", h + 2*t }')

if ! width=$(mm_to_px "$total_width_mm") || ! height=$(mm_to_px "$total_height_mm") ||
   ! x_trim=$(mm_to_px "$trim") ||
   ! x_cover1_end=$(mm_to_px "$(awk -v t="$trim" -v w="$cover_width" 'BEGIN { printf "%.12f", t+w }')") ||
   ! x_spine_end=$(mm_to_px "$(awk -v t="$trim" -v w="$cover_width" -v s="$spine_width" 'BEGIN { printf "%.12f", t+w+s }')") ||
   ! x_cover2_end=$(mm_to_px "$(awk -v t="$trim" -v w="$cover_width" -v s="$spine_width" 'BEGIN { printf "%.12f", t+2*w+s }')") ||
   ! y_trim=$(mm_to_px "$trim") ||
   ! y_cover_end=$(mm_to_px "$(awk -v t="$trim" -v h="$cover_height" 'BEGIN { printf "%.12f", t+h }')") ||
   ! y_frame_inner_top=$(mm_to_px "$(awk -v t="$trim" -v b="$bleed" 'BEGIN { printf "%.12f", t+b }')") ||
   ! y_frame_inner_bottom=$(mm_to_px "$(awk -v t="$trim" -v h="$cover_height" -v b="$bleed" 'BEGIN { printf "%.12f", t+h-b }')") ||
   ! x_cover1_frame_inner_left=$(mm_to_px "$(awk -v t="$trim" -v b="$bleed" 'BEGIN { printf "%.12f", t+b }')") ||
   ! x_cover1_frame_inner_right=$(mm_to_px "$(awk -v t="$trim" -v w="$cover_width" -v b="$bleed" 'BEGIN { printf "%.12f", t+w-b }')") ||
   ! x_cover2_frame_inner_left=$(mm_to_px "$(awk -v t="$trim" -v w="$cover_width" -v s="$spine_width" -v b="$bleed" 'BEGIN { printf "%.12f", t+w+s+b }')") ||
   ! x_cover2_frame_inner_right=$(mm_to_px "$(awk -v t="$trim" -v w="$cover_width" -v s="$spine_width" -v b="$bleed" 'BEGIN { printf "%.12f", t+2*w+s-b }')"); then
    echo 'エラー: 寸法が大きすぎるか、ピクセルに変換できません。' >&2
    exit 1
fi
if ((width < 1 || height < 1)); then
    echo 'エラー: 出力画像の寸法が小さすぎてピクセルに変換できません。' >&2
    exit 1
fi

if command -v magick >/dev/null 2>&1; then
    imagemagick=(magick)
elif command -v convert >/dev/null 2>&1; then
    imagemagick=(convert)
else
    echo 'エラー: ImageMagick が見つかりません。ImageMagick 6または7をインストールしてください。' >&2
    exit 1
fi

output_dir=$(dirname -- "$output_path")
output_name=$(basename -- "$output_path")
if ! output_dir=$(cd -- "$output_dir" 2>/dev/null && pwd -P); then
    echo "エラー: 出力先ディレクトリにアクセスできません: $(dirname -- "$output_path")" >&2
    exit 1
fi
output_file=$output_dir/$output_name

draw_rect() {
    local color=$1 x1=$2 y1=$3 x2=$4 y2=$5
    local right=$((x2 - 1)) bottom=$((y2 - 1))
    if ((right < x1 || bottom < y1)); then
        return 0
    fi
    image_args+=(-fill "$color" -draw "rectangle $x1,$y1 $right,$bottom")
}

draw_frame() {
    local left=$1 right=$2 top=$3 bottom=$4
    local inner_left=$5 inner_right=$6 inner_top=$7 inner_bottom=$8
    draw_rect orange "$left" "$top" "$right" "$inner_top"
    draw_rect orange "$left" "$inner_bottom" "$right" "$bottom"
    draw_rect orange "$left" "$inner_top" "$inner_left" "$inner_bottom"
    draw_rect orange "$inner_right" "$inner_top" "$right" "$inner_bottom"
}

image_args=(-size "${width}x${height}" xc:red)
draw_rect green "$x_trim" "$y_trim" "$x_cover1_end" "$y_cover_end"
draw_rect blue "$x_cover1_end" "$y_trim" "$x_spine_end" "$y_cover_end"
draw_rect green "$x_spine_end" "$y_trim" "$x_cover2_end" "$y_cover_end"
if awk -v b="$bleed" 'BEGIN { exit !(b > 0) }'; then
    draw_frame "$x_trim" "$x_cover1_end" "$y_trim" "$y_cover_end" \
        "$x_cover1_frame_inner_left" "$x_cover1_frame_inner_right" "$y_frame_inner_top" "$y_frame_inner_bottom"
    draw_rect orange "$x_cover1_end" "$y_trim" "$x_spine_end" "$y_frame_inner_top"
    draw_rect orange "$x_cover1_end" "$y_frame_inner_bottom" "$x_spine_end" "$y_cover_end"
    draw_frame "$x_spine_end" "$x_cover2_end" "$y_trim" "$y_cover_end" \
        "$x_cover2_frame_inner_left" "$x_cover2_frame_inner_right" "$y_frame_inner_top" "$y_frame_inner_bottom"
fi

"${imagemagick[@]}" "${image_args[@]}" -units PixelsPerInch -density "$dpi" -quality 100 "jpg:$output_file"
printf '画像を作成しました: %s (%s dpi, %sx%s px)\n' "$output_file" "$dpi" "$width" "$height"
