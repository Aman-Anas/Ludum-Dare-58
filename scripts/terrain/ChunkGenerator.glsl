#[compute]
#version 460

// Purpose of this compute shader is to generate geometry for a chunk


// --------- Noise function to use for unmodified terrain-------
// psrdnoise (c) Stefan Gustavson and Ian McEwan,
// ver. 2021-12-02, published under the MIT license:
// https://github.com/stegu/psrdnoise/

vec4 permute(vec4 i) {
     vec4 im = mod(i, 289.0);
     return mod(((im*34.0)+10.0)*im, 289.0);
}

float psrdnoise(vec3 x, vec3 period, float alpha, out vec3 gradient)
{
  const mat3 M = mat3(0.0, 1.0, 1.0, 1.0, 0.0, 1.0,  1.0, 1.0, 0.0);
  const mat3 Mi = mat3(-0.5, 0.5, 0.5, 0.5,-0.5, 0.5, 0.5, 0.5,-0.5);
  vec3 uvw = M * x;
  vec3 i0 = floor(uvw), f0 = fract(uvw);
  vec3 g_ = step(f0.xyx, f0.yzz), l_ = 1.0 - g_;
  vec3 g = vec3(l_.z, g_.xy), l = vec3(l_.xy, g_.z);
  vec3 o1 = min( g, l ), o2 = max( g, l );
  vec3 i1 = i0 + o1, i2 = i0 + o2, i3 = i0 + vec3(1.0);
  vec3 v0 = Mi * i0, v1 = Mi * i1, v2 = Mi * i2, v3 = Mi * i3;
  vec3 x0 = x - v0, x1 = x - v1, x2 = x - v2, x3 = x - v3;
  if(any(greaterThan(period, vec3(0.0)))) {
    vec4 vx = vec4(v0.x, v1.x, v2.x, v3.x);
    vec4 vy = vec4(v0.y, v1.y, v2.y, v3.y);
    vec4 vz = vec4(v0.z, v1.z, v2.z, v3.z);
	if(period.x > 0.0) vx = mod(vx, period.x);
	if(period.y > 0.0) vy = mod(vy, period.y);
	if(period.z > 0.0) vz = mod(vz, period.z);
	i0 = floor(M * vec3(vx.x, vy.x, vz.x) + 0.5);
	i1 = floor(M * vec3(vx.y, vy.y, vz.y) + 0.5);
	i2 = floor(M * vec3(vx.z, vy.z, vz.z) + 0.5);
	i3 = floor(M * vec3(vx.w, vy.w, vz.w) + 0.5);
  }
  vec4 hash = permute( permute( permute( 
              vec4(i0.z, i1.z, i2.z, i3.z ))
            + vec4(i0.y, i1.y, i2.y, i3.y ))
            + vec4(i0.x, i1.x, i2.x, i3.x ));
  vec4 theta = hash * 3.883222077;
  vec4 sz = hash * -0.006920415 + 0.996539792;
  vec4 psi = hash * 0.108705628;
  vec4 Ct = cos(theta), St = sin(theta);
  vec4 sz_prime = sqrt( 1.0 - sz*sz );
  vec4 gx, gy, gz;
  if(alpha != 0.0) {
    vec4 px = Ct * sz_prime, py = St * sz_prime, pz = sz;
    vec4 Sp = sin(psi), Cp = cos(psi), Ctp = St*Sp - Ct*Cp;
    vec4 qx = mix( Ctp*St, Sp, sz), qy = mix(-Ctp*Ct, Cp, sz);
    vec4 qz = -(py*Cp + px*Sp);
    vec4 Sa = vec4(sin(alpha)), Ca = vec4(cos(alpha));
    gx = Ca*px + Sa*qx; gy = Ca*py + Sa*qy; gz = Ca*pz + Sa*qz;
  }
  else {
    gx = Ct * sz_prime; gy = St * sz_prime; gz = sz;  
  }
  vec3 g0 = vec3(gx.x, gy.x, gz.x), g1 = vec3(gx.y, gy.y, gz.y);
  vec3 g2 = vec3(gx.z, gy.z, gz.z), g3 = vec3(gx.w, gy.w, gz.w);
  vec4 w = 0.5-vec4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3));
  w = max(w, 0.0); vec4 w2 = w * w, w3 = w2 * w;
  vec4 gdotx = vec4(dot(g0,x0), dot(g1,x1), dot(g2,x2), dot(g3,x3));
  float n = dot(w3, gdotx);
  vec4 dw = -6.0 * w2 * gdotx;
  vec3 dn0 = w3.x * g0 + dw.x * x0;
  vec3 dn1 = w3.y * g1 + dw.y * x1;
  vec3 dn2 = w3.z * g2 + dw.z * x2;
  vec3 dn3 = w3.w * g3 + dw.w * x3;
  gradient = 39.5 * (dn0 + dn1 + dn2 + dn3);
  return 39.5 * n;
}

// --------- Struct definitions -------

// Making all of these separate floats to ensure it doesn't do some goofy
// vec3 packing alignment stuff
struct Vertex
{
    float posX;
    float posY;
    float posZ;

    float normX;
    float normY;
    float normZ;
};
struct Triangle
{
    Vertex a;
    Vertex b;
    Vertex c;
};


// --------- Marching Cubes data (thanks to Sebastian Lague :D) --------
const int cornerIndexAFromEdge[12] = {0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3};
const int cornerIndexBFromEdge[12] = {1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7};

const int offsets[256] = {0, 0, 3, 6, 12, 15, 21, 27, 36, 39, 45, 51, 60, 66, 75, 84, 90, 93, 99, 105, 114, 120, 129, 138, 150, 156, 165, 174, 186, 195, 207, 219, 228, 231, 237, 243, 252, 258, 267, 276, 288, 294, 303, 312, 324, 333, 345, 357, 366, 372, 381, 390, 396, 405, 417, 429, 438, 447, 459, 471, 480, 492, 507, 522, 528, 531, 537, 543, 552, 558, 567, 576, 588, 594, 603, 612, 624, 633, 645, 657, 666, 672, 681, 690, 702, 711, 723, 735, 750, 759, 771, 783, 798, 810, 825, 840, 852, 858, 867, 876, 888, 897, 909, 915, 924, 933, 945, 957, 972, 984, 999, 1008, 1014, 1023, 1035, 1047, 1056, 1068, 1083, 1092, 1098, 1110, 1125, 1140, 1152, 1167, 1173, 1185, 1188, 1191, 1197, 1203, 1212, 1218, 1227, 1236, 1248, 1254, 1263, 1272, 1284, 1293, 1305, 1317, 1326, 1332, 1341, 1350, 1362, 1371, 1383, 1395, 1410, 1419, 1425, 1437, 1446, 1458, 1467, 1482, 1488, 1494, 1503, 1512, 1524, 1533, 1545, 1557, 1572, 1581, 1593, 1605, 1620, 1632, 1647, 1662, 1674, 1683, 1695, 1707, 1716, 1728, 1743, 1758, 1770, 1782, 1791, 1806, 1812, 1827, 1839, 1845, 1848, 1854, 1863, 1872, 1884, 1893, 1905, 1917, 1932, 1941, 1953, 1965, 1980, 1986, 1995, 2004, 2010, 2019, 2031, 2043, 2058, 2070, 2085, 2100, 2106, 2118, 2127, 2142, 2154, 2163, 2169, 2181, 2184, 2193, 2205, 2217, 2232, 2244, 2259, 2268, 2280, 2292, 2307, 2322, 2328, 2337, 2349, 2355, 2358, 2364, 2373, 2382, 2388, 2397, 2409, 2415, 2418, 2427, 2433, 2445, 2448, 2454, 2457, 2460};
const int lengths[256] = {0, 3, 3, 6, 3, 6, 6, 9, 3, 6, 6, 9, 6, 9, 9, 6, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 6, 9, 9, 6, 9, 12, 12, 9, 9, 12, 12, 9, 12, 15, 15, 6, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 6, 9, 9, 12, 9, 12, 12, 15, 9, 12, 12, 15, 12, 15, 15, 12, 6, 9, 9, 12, 9, 12, 6, 9, 9, 12, 12, 15, 12, 15, 9, 6, 9, 12, 12, 9, 12, 15, 9, 6, 12, 15, 15, 12, 15, 6, 12, 3, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 6, 9, 9, 12, 9, 12, 12, 15, 9, 6, 12, 9, 12, 9, 15, 6, 6, 9, 9, 12, 9, 12, 12, 15, 9, 12, 12, 15, 12, 15, 15, 12, 9, 12, 12, 9, 12, 15, 15, 12, 12, 9, 15, 6, 15, 12, 6, 3, 6, 9, 9, 12, 9, 12, 12, 15, 9, 12, 12, 15, 6, 9, 9, 6, 9, 12, 12, 15, 12, 15, 15, 6, 12, 9, 15, 12, 9, 6, 12, 3, 9, 12, 12, 15, 12, 15, 9, 12, 12, 15, 15, 6, 9, 12, 6, 3, 6, 9, 9, 6, 9, 12, 6, 3, 9, 6, 12, 3, 6, 3, 3, 0};


// ------- Buffers for our data -----------

layout(set = 0, binding = 0, std430) restrict buffer TriangleBuffer
{
	Triangle data[];
} triangleBuffer; // Geometry buffer

layout(set = 0, binding = 1, std430) restrict buffer ParamsBuffer
{
	float noiseScale;
	float isoLevel;
	int numVoxelsPerAxis;
	float chunkScale;
	float chunkX;
	float chunkY;
	float chunkZ;
	float noiseOffsetX;
	float noiseOffsetY;
	float noiseOffsetZ;
    int useMods; // negative if don't positive if do
} params; // Parameters for the chunk and generation

layout(set = 0, binding = 2, std430) coherent buffer Counter
{
	uint counter;
}; // Counter for number of triangles

layout(set = 0, binding = 3, std430) restrict buffer LutBuffer
{
	int data[];
} lut;

layout(set = 0, binding = 4, std430) restrict buffer ModBuffer
{
	int data[];
} modData;

float sdBox( vec3 p, vec3 b )
{
  vec3 q = abs(p) - b;
  return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
}

float sdSphere( vec3 p, float s )
{
  return length(p)-s;
}

vec4 evaluate(ivec3 coord)
{   
    vec3 grad;

    vec3 chunkPos = vec3(params.chunkX, params.chunkY, params.chunkZ);
    vec3 noiseOffset = vec3(params.noiseOffsetX, params.noiseOffsetY, params.noiseOffsetZ);

    vec3 percentPos = (coord / vec3(params.numVoxelsPerAxis)) - vec3(0.5); // -0.5 to 0.5 position relative to chunk

    vec3 chunkWorldPos = (percentPos * params.chunkScale);
    vec3 trueWorldPos = chunkWorldPos + (chunkPos * params.chunkScale);
    vec3 samplePos = (percentPos + chunkPos) * params.noiseScale + noiseOffset;


	float sum = 0;
	float amplitude = 1;
	float weight = 1;
  
    vec3 fbmSample = samplePos;

	for (int i = 0; i < 3; i ++)//6
	{
		float noise = psrdnoise(fbmSample, vec3(0.0), 0.0, grad);// * 2 - 1;
		//noise = 1 - abs(noise);
		// noise *= noise;
		noise *= weight;
		weight = max(0, min(1, noise * 10));
		sum += noise * amplitude;
		fbmSample *= 2;
		amplitude *= 0.5;
	}
	float density = clamp(sum, 0.0, 1.0);
    
    density = sdSphere(samplePos, 2) + density;


    // float sphere = clamp(-sdSphere(samplePos, 1), -2.0, 1.0);
    // density = -density;
    // density = density + 0.1 * min((length(trueWorldPos)-1), 1);
    // density = (density + 1)/2;
    // density = sphere;// / 2;//, 0.0, 1.0);
    // density = (density*2)-1;
    // density = -samplePos.y;// - density;
    // float posSum = (sum + 1.0); // Sum from 0 to 1
    // float dist = clamp(length(samplePos), 0, 1);
    // density -= (dist*dist*dist * posSum);
    // density += sphere;
    // density =  clamp(-distanceFromSphere, -1.0, 1.0) - tunnels;
    // density = density;
    // density += -distanceFromOrigin/10;
    // density = -sdBox(trueWorldPos, vec3(4, 3, 4));
    // density = (-trueWorldPos.y * density) - density;
	// density = -(worldPos.y+100)/300 + density;
    // density = (-trueWorldPos.y / 100) - density*2;
    // density = 1 / length(trueWorldPos); // funny smol sphere
    // density = 0.1 + length(trueWorldPos);// - density;;
	return vec4(chunkWorldPos, density);
}

// int indexFromCoord(ivec3 coord) {
//     int numVoxelsPerAxis = params.numVoxelsPerAxis;
// 	return (coord.z * numVoxelsPerAxis * numVoxelsPerAxis) + (coord.y * numVoxelsPerAxis) + coord.x;
// }

vec3 calculateNormal(ivec3 coord) {
	ivec3 offsetX = ivec3(1, 0, 0);
	ivec3 offsetY = ivec3(0, 1, 0);
	ivec3 offsetZ = ivec3(0, 0, 1);

	float dx = evaluate(coord + offsetX).w - evaluate(coord - offsetX).w;
	float dy = evaluate(coord + offsetY).w - evaluate(coord - offsetY).w;
	float dz = evaluate(coord + offsetZ).w - evaluate(coord - offsetZ).w;

	return normalize(vec3(dx, dy, dz));
}

Vertex generateVertex(ivec3 localPosA, ivec3 localPosB, float isoLevel)
{
	
    Vertex v;
    vec4 v1 = evaluate(localPosA);
    vec4 v2 = evaluate(localPosB);

	float t = (isoLevel - v1.w) / (v2.w - v1.w);
    // vec4 position =  (v1 + v2) * 0.5; // blocky version
	vec4 position = v1 + t * (v2 - v1);

    vec3 normalA = calculateNormal(localPosA);
	vec3 normalB = calculateNormal(localPosB);
	vec3 normal = normalize(normalA + t * (normalB - normalA));

    v.posX = position.x;
    v.posY = position.y;
    v.posZ = position.z;

    v.normX = normal.x;
    v.normY = normal.y;
    v.normZ = normal.z;
    return v;
}

// 8, 8, 8 was original
layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;
void main()
{
	vec3 id = gl_GlobalInvocationID;

	// 8 corners of the current cube
	ivec3 cubeCorners[8] = {
		ivec3(id.x + 0, id.y + 0, id.z + 0),
		ivec3(id.x + 1, id.y + 0, id.z + 0),
		ivec3(id.x + 1, id.y + 0, id.z + 1),
		ivec3(id.x + 0, id.y + 0, id.z + 1),
		ivec3(id.x + 0, id.y + 1, id.z + 0),
		ivec3(id.x + 1, id.y + 1, id.z + 0),
		ivec3(id.x + 1, id.y + 1, id.z + 1),
		ivec3(id.x + 0, id.y + 1, id.z + 1)
	};

	// Calculate unique index for each cube configuration.
	// There are 256 possible values
	// A value of 0 means cube is entirely inside surface; 255 entirely outside.
	// The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
	uint cubeIndex = 0;
	float isoLevel = params.isoLevel;
	if (evaluate(cubeCorners[0]).w < isoLevel) cubeIndex |= 1;
	if (evaluate(cubeCorners[1]).w < isoLevel) cubeIndex |= 2;
	if (evaluate(cubeCorners[2]).w < isoLevel) cubeIndex |= 4;
	if (evaluate(cubeCorners[3]).w < isoLevel) cubeIndex |= 8;
	if (evaluate(cubeCorners[4]).w < isoLevel) cubeIndex |= 16;
	if (evaluate(cubeCorners[5]).w < isoLevel) cubeIndex |= 32;
	if (evaluate(cubeCorners[6]).w < isoLevel) cubeIndex |= 64;
	if (evaluate(cubeCorners[7]).w < isoLevel) cubeIndex |= 128;

	// Create triangles for current cube configuration
	int numIndices = lengths[cubeIndex];
	int offset = offsets[cubeIndex];
	
	for (int i = 0; i < numIndices; i += 3) {
		// Get indices of corner points A and B for each of the three edges
		// of the cube that need to be joined to form the triangle.
		int v0 = lut.data[offset + i];
		int v1 = lut.data[offset + 1 + i];
		int v2 = lut.data[offset + 2 + i];

		int a0 = cornerIndexAFromEdge[v0];
		int b0 = cornerIndexBFromEdge[v0];

		int a1 = cornerIndexAFromEdge[v1];
		int b1 = cornerIndexBFromEdge[v1];

		int a2 = cornerIndexAFromEdge[v2];
		int b2 = cornerIndexBFromEdge[v2];

		
		// Calculate vertex positions
		Triangle currTri;
		currTri.c = generateVertex(cubeCorners[a0], cubeCorners[b0], isoLevel);
		currTri.b = generateVertex(cubeCorners[a1], cubeCorners[b1], isoLevel);
		currTri.a = generateVertex(cubeCorners[a2], cubeCorners[b2], isoLevel);

		// vec3 ab = currTri.b.xyz - currTri.a.xyz;
		// vec3 ac = currTri.c.xyz - currTri.a.xyz;
		// currTri.norm = -vec4(normalize(cross(ab,ac)), 0);

		uint index = atomicAdd(counter, 1u);
		triangleBuffer.data[index] = currTri;
		
	}
}