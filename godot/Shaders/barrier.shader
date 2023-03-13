shader_type canvas_item;

uniform float destructionProgress = 0.0; // 0.0 means not destroyed, 1.0 means fully destroyed

// Used by the noise functin to generate a pseudo random value between 0.0 and 1.0
vec2 random(vec2 uv){
    uv = vec2( dot(uv, vec2(127.1,311.7) ),
               dot(uv, vec2(269.5,183.3) ) );
    return -1.0 + 2.0 * fract(sin(uv) * 43758.5453123);
}

vec4 applyFizzle(vec4 color, vec2 uv) {
    float fizzle = destructionProgress;
    //fizzle = max(0.0, sin(TIME * 2.2));
    vec2 rand2 = random(ceil(uv * 30.0)) + 1.0;
    rand2 *= 0.5;
    if (rand2.x < fizzle) {
        color.a = 0.0;
    } else {
        vec3 headroom = vec3(1.0, 1.0, 1.0) - color.rgb;
        headroom = min(vec3(0.6, 0.6, 0.6), headroom);
        color.rgb += headroom * fizzle * fizzle;
    }
    return color;
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    vec4 color = tex;
    color = applyFizzle(color, UV);
    COLOR = color;
}