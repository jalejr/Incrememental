shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;

uniform sampler2D number_atlas : source_color;

void fragment() {
	float damage = COLOR.r * 1000.0;  // Unpack (0-999)
	float is_critical = COLOR.g;
	float alpha = COLOR.a;
	
	float uv_scaled = UV.x * 3.0;
	int digit_index = int(uv_scaled);
	float local_uv = fract(uv_scaled);
	
	int damage_int = int(round(damage));
	int hundreds = (damage_int / 100) % 10;
	int tens = (damage_int / 10) % 10;
	int ones = damage_int % 10;
	
	float digit_float = float(ones);  // Default to ones
	digit_float = mix(digit_float, float(tens), float(digit_index == 1));
	digit_float = mix(digit_float, float(hundreds), float(digit_index == 0));
	int digit = int(digit_float);
	
	float show_this_digit = 1.0;
	show_this_digit *= mix(1.0, float(damage_int >= 100), float(digit_index == 0));
	show_this_digit *= mix(1.0, float(damage_int >= 10), float(digit_index == 1));

	float atlas_u = (float(digit) + local_uv) * 0.1;  // 0.1 = 1/10 digits

	float row_height = 1.0;
	float atlas_v = fract(UV.y) * row_height;
	atlas_v += is_critical * row_height;
	
	vec2 atlas_uv = vec2(atlas_u, atlas_v);
	vec4 tex = texture(number_atlas, atlas_uv);
	
	ALBEDO = tex.rgb;
	ALPHA = tex.a * alpha * show_this_digit;
}