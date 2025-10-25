shader_type particles;

uniform sampler2D number_atlas : source_color;

uniform int digits_per_row = 10;  // 0-9 in one row
uniform int total_rows = 1;       // Normal
uniform float digit_width = 0.1;  // 1.0 / 10 = 0.1 (10 digits)

void start() {
	CUSTOM.a = TIME;

	//float horizontal_offset = CUSTOM.a;
	//TRANSFORM[3].x += horizontal_offset;
}

void process() {
	float elapsed = TIME - CUSTOM.a;
	float progress = elapsed / LIFETIME;

	ACTIVE = progress < 1.0;


	VELOCITY.y = 2.0 * (1.0 - progress * 0.5);  // Slow down over time

	TRANSFORM[3].xyz += VELOCITY * DELTA;

	float alpha = (1.0 - smoothstep(0.7, 1.0, progress));

	float is_critical = CUSTOM.g;
	float is_heal = CUSTOM.b;

	float scale = mix(1.0, 1.5, is_critical);

	float pulse = sin(progress * 3.14159 * 2.0) * 0.1;
	scale += pulse * is_critical;
	scale = mix(scale, 0.9, is_heal);
	TRANSFORM[0].xyz *= scale;
	TRANSFORM[1].xyz *= scale;
	TRANSFORM[2].xyz *= scale;

	float damage = CUSTOM.r;

	COLOR.r = damage / 1000.0;
	COLOR.g = is_critical;
	COLOR.b = 0.0;  // Unused
	COLOR.a = alpha;
}
