#version 330

in vec3 fragWorldPos;

out vec4 finalColor;

uniform samplerCube environmentMap;
uniform vec4 colDiffuse;
uniform vec3 uCamPos;

void main() {
    vec3 dir = normalize(fragWorldPos - uCamPos);
    vec3 color = texture(environmentMap, dir).rgb;
    finalColor = vec4(color, 1.0) * colDiffuse;
}
