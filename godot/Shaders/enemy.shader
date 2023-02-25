shader_type canvas_item;

uniform vec3 my_color = vec3(1.0, .102, 0.47);
uniform float my_alpha = 1.0;
uniform float is_corrupt = 1.0; // boolean, 0.0 or 1.0
uniform float destructionProgress = 0.0; // 0.0 means not destroyed, 1.0 means fully destroyed
//const vec3 corruptColor = vec3(0.025, .19, 0.11);
const vec3 corruptColor = vec3(0.025, .1, 0.05);

const float r1 = 0.21;
const float r2 = 0.25;

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

float getGlow(float glow, float width, vec2 uv, float y, vec2 screenUV) {
    float y2 = mod(uv.y + TIME / 6.0, 1.0);
    return max(glow, 1.0 - smoothstep(0.0, width, abs(y2 - y)));
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    vec4 color = tex;
    color.rgb = my_color * color.rgb;
    color.a = min(color.a, my_alpha);

    if (1.0 < 0.0 && abs(0.5 - UV.x) < 0.3 && abs(0.5 - UV.y) < 0.3 && tex.a < 0.3) {
        float dist = distance(UV, vec2(0.5, 0.5));
        float limit = 0.2 + 0.7 * abs(sin(TIME));
        float glow = 0.0;
        glow = getGlow(glow, 0.08, UV, 0.2, SCREEN_UV);
        glow = getGlow(glow, 0.014, UV, 0.31, SCREEN_UV);
        glow = getGlow(glow, 0.08, UV, 0.48, SCREEN_UV);
        glow = getGlow(glow, 0.014, UV, 0.68, SCREEN_UV);
        glow = getGlow(glow, 0.06, UV, 0.8, SCREEN_UV);
        glow = getGlow(glow, 0.09, UV, 0.88, SCREEN_UV);
        glow = getGlow(glow, 0.04, UV, 0.96, SCREEN_UV);

        vec4 corruption = vec4(corruptColor, 1.0);
        corruption = mix(corruption, vec4(corruptColor, 1.0) * (1.0 + glow * 0.6), glow);
        color = corruption;
    }

    if (1.0 < 0.0) {
        float glow = smoothstep(0.15, 0.75, abs(sin(TIME * 2.8))) * 0.3 + 0.7;
        vec4 corruption = vec4(corruptColor, 1.0);
        corruption = mix(corruption, vec4(corruptColor, 1.0) * (1.3 + glow * 0.3), glow);

        float blah = max(abs(0.5 - UV.x), abs(0.5 - UV.y));
        blah = max(blah, distance(vec2(0.5, 0.5), UV) * 0.84);
        blah = 1.0 - smoothstep(0.2, 0.24, blah);
        color = mix(color, corruption, blah * is_corrupt);
    }

    if (1.0 < 0.0) {
        vec2 rand2 = random(ceil(UV * 90.0) * TIME / 400000000.0) + 1.0;
        rand2 *= 0.5;
        if (is_corrupt > 0.5 && rand2.x < 0.8 && rand2.y < 0.8) {
            float blah = max(abs(0.5 - UV.x), abs(0.5 - UV.y));
            blah = max(blah, distance(vec2(0.5, 0.5), UV) * 0.84);
            blah = 1.0 - smoothstep(0.2, 0.24, blah);

            color = mix(color, vec4(corruptColor * 1.6, 1.0), blah);
        }
    }

    if (1.0 < 0.0) {
        float blah = max(abs(0.5 - UV.x), abs(0.5 - UV.y));
        blah = max(blah, distance(vec2(0.5, 0.5), UV) * 0.84);
        float glow = smoothstep(0.1, 0.24, blah) * abs(sin(TIME * 2.0));
        blah = 1.0 - smoothstep(0.2, 0.24, blah);
        vec3 corruption = corruptColor;
        vec3 headroom = my_color - corruption;
        corruption += headroom * glow * 1.0;
        color = mix(color, vec4(corruption, 1.0), blah * max(1.0, is_corrupt)); // make them all corrupt
    }

    if (1.0 > 0.0) {
        // Only mix onto black pixels.
        // Remember that tex.r == tex.g == tex.b
        if (tex.r < 0.1 && tex.a > 0.5) {
            vec2 rand2 = random(ceil(UV * 20.0) + TIME / 200000.0) + 1.0;
            float mixer = step(1.2, rand2.y);
            vec4 toMix = vec4(0.0, 0.0, 0.0, 0.0);
            toMix = vec4(my_color * 0.6, 1.0); // TODO we could allow the bitmap to control this
            color = mix(toMix, vec4(corruptColor, color.a), mixer);
        }
    }

    color = applyFizzle(color, UV);

    COLOR = color;
}