#[compute]
#version 460

// #------ SIMPLEX NOISE ------#
// Description : Array and textureless GLSL 2D/3D/4D simplex 
//               noise functions.
//      Author : Ian McEwan, Ashima Arts.
//  Maintainer : stegu
//     Lastmod : 20201014 (stegu)
//     License : Copyright (C) 2011 Ashima Arts. All rights reserved.
//               Distributed under the MIT License. See LICENSE file.
//               https://github.com/ashima/webgl-noise
//               https://github.com/stegu/webgl-noise
vec3 mod289(vec3 x) {
	return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 mod289(vec4 x) {
	return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 permute(vec4 x) {
	return mod289(((x*34.0)+10.0)*x);
}

vec4 taylorInvSqrt(vec4 r)
{
	return 1.79284291400159 - 0.85373472095314 * r;
}

float snoise(vec3 v)
{ 
	const vec2  C = vec2(1.0/6.0, 1.0/3.0) ;
	const vec4  D = vec4(0.0, 0.5, 1.0, 2.0);

	// First corner
	vec3 i  = floor(v + dot(v, C.yyy) );
	vec3 x0 =   v - i + dot(i, C.xxx) ;

	// Other corners
	vec3 g = step(x0.yzx, x0.xyz);
	vec3 l = 1.0 - g;
	vec3 i1 = min( g.xyz, l.zxy );
	vec3 i2 = max( g.xyz, l.zxy );

	//   x0 = x0 - 0.0 + 0.0 * C.xxx;
	//   x1 = x0 - i1  + 1.0 * C.xxx;
	//   x2 = x0 - i2  + 2.0 * C.xxx;
	//   x3 = x0 - 1.0 + 3.0 * C.xxx;
	vec3 x1 = x0 - i1 + C.xxx;
	vec3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y
	vec3 x3 = x0 - D.yyy;      // -1.0+3.0*C.x = -0.5 = -D.y

	// Permutations
	i = mod289(i); 
	vec4 p = permute( permute( permute( 
			i.z + vec4(0.0, i1.z, i2.z, 1.0 ))
			+ i.y + vec4(0.0, i1.y, i2.y, 1.0 )) 
			+ i.x + vec4(0.0, i1.x, i2.x, 1.0 ));

	// Gradients: 7x7 points over a square, mapped onto an octahedron.
	// The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
	float n_ = 0.142857142857; // 1.0/7.0
	vec3  ns = n_ * D.wyz - D.xzx;

	vec4 j = p - 49.0 * floor(p * ns.z * ns.z);  //  mod(p,7*7)

	vec4 x_ = floor(j * ns.z);
	vec4 y_ = floor(j - 7.0 * x_ );    // mod(j,N)

	vec4 x = x_ *ns.x + ns.yyyy;
	vec4 y = y_ *ns.x + ns.yyyy;
	vec4 h = 1.0 - abs(x) - abs(y);

	vec4 b0 = vec4( x.xy, y.xy );
	vec4 b1 = vec4( x.zw, y.zw );

	vec4 s0 = floor(b0)*2.0 + 1.0;
	vec4 s1 = floor(b1)*2.0 + 1.0;
	vec4 sh = -step(h, vec4(0.0));

	vec4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ;
	vec4 a1 = b1.xzyw + s1.xzyw*sh.zzww ;

	vec3 p0 = vec3(a0.xy,h.x);
	vec3 p1 = vec3(a0.zw,h.y);
	vec3 p2 = vec3(a1.xy,h.z);
	vec3 p3 = vec3(a1.zw,h.w);

	//Normalise gradients
	vec4 norm = taylorInvSqrt(vec4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3)));
	p0 *= norm.x;
	p1 *= norm.y;
	p2 *= norm.z;
	p3 *= norm.w;

	// Mix final noise value
	vec4 m = max(0.5 - vec4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
	m = m * m;
	return 105.0 * dot( m*m, vec4( dot(p0,x0), dot(p1,x1), 
								dot(p2,x2), dot(p3,x3) ) );
	}

struct Triangle {
	vec4 a; // 4 floats
	vec4 b; // 4 floats
	vec4 c; // 4 floats
	vec4 norm; // 4 floats
};

// #------ Marching Cubes ------#
const int cornerIndexAFromEdge[12] = {0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3};
const int cornerIndexBFromEdge[12] = {1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7};

const int offsets[256] = {0, 0, 3, 6, 12, 15, 21, 27, 36, 39, 45, 51, 60, 66, 75, 84, 90, 93, 99, 105, 114, 120, 129, 138, 150, 156, 165, 174, 186, 195, 207, 219, 228, 231, 237, 243, 252, 258, 267, 276, 288, 294, 303, 312, 324, 333, 345, 357, 366, 372, 381, 390, 396, 405, 417, 429, 438, 447, 459, 471, 480, 492, 507, 522, 528, 531, 537, 543, 552, 558, 567, 576, 588, 594, 603, 612, 624, 633, 645, 657, 666, 672, 681, 690, 702, 711, 723, 735, 750, 759, 771, 783, 798, 810, 825, 840, 852, 858, 867, 876, 888, 897, 909, 915, 924, 933, 945, 957, 972, 984, 999, 1008, 1014, 1023, 1035, 1047, 1056, 1068, 1083, 1092, 1098, 1110, 1125, 1140, 1152, 1167, 1173, 1185, 1188, 1191, 1197, 1203, 1212, 1218, 1227, 1236, 1248, 1254, 1263, 1272, 1284, 1293, 1305, 1317, 1326, 1332, 1341, 1350, 1362, 1371, 1383, 1395, 1410, 1419, 1425, 1437, 1446, 1458, 1467, 1482, 1488, 1494, 1503, 1512, 1524, 1533, 1545, 1557, 1572, 1581, 1593, 1605, 1620, 1632, 1647, 1662, 1674, 1683, 1695, 1707, 1716, 1728, 1743, 1758, 1770, 1782, 1791, 1806, 1812, 1827, 1839, 1845, 1848, 1854, 1863, 1872, 1884, 1893, 1905, 1917, 1932, 1941, 1953, 1965, 1980, 1986, 1995, 2004, 2010, 2019, 2031, 2043, 2058, 2070, 2085, 2100, 2106, 2118, 2127, 2142, 2154, 2163, 2169, 2181, 2184, 2193, 2205, 2217, 2232, 2244, 2259, 2268, 2280, 2292, 2307, 2322, 2328, 2337, 2349, 2355, 2358, 2364, 2373, 2382, 2388, 2397, 2409, 2415, 2418, 2427, 2433, 2445, 2448, 2454, 2457, 2460};
const int lengths[256] = {0, 3, 3, 6, 3, 6, 6, 9, 3, 6, 6, 9, 6, 9, 9, 6, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 6, 9, 9, 6, 9, 12, 12, 9, 9, 12, 12, 9, 12, 15, 15, 6, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 6, 9, 9, 12, 9, 12, 12, 15, 9, 12, 12, 15, 12, 15, 15, 12, 6, 9, 9, 12, 9, 12, 6, 9, 9, 12, 12, 15, 12, 15, 9, 6, 9, 12, 12, 9, 12, 15, 9, 6, 12, 15, 15, 12, 15, 6, 12, 3, 3, 6, 6, 9, 6, 9, 9, 12, 6, 9, 9, 12, 9, 12, 12, 9, 6, 9, 9, 12, 9, 12, 12, 15, 9, 6, 12, 9, 12, 9, 15, 6, 6, 9, 9, 12, 9, 12, 12, 15, 9, 12, 12, 15, 12, 15, 15, 12, 9, 12, 12, 9, 12, 15, 15, 12, 12, 9, 15, 6, 15, 12, 6, 3, 6, 9, 9, 12, 9, 12, 12, 15, 9, 12, 12, 15, 6, 9, 9, 6, 9, 12, 12, 15, 12, 15, 15, 6, 12, 9, 15, 12, 9, 6, 12, 3, 9, 12, 12, 15, 12, 15, 9, 12, 12, 15, 15, 6, 9, 12, 6, 3, 6, 9, 9, 6, 9, 12, 6, 3, 9, 6, 12, 3, 6, 3, 3, 0};

layout(set = 0, binding = 0, std430) restrict buffer TriangleBuffer
{
	Triangle data[];
}
triangleBuffer;

layout(set = 0, binding = 1, std430) restrict buffer ParamsBuffer
{
	float time;
	float noiseScale;
	float isoLevel;
	float numVoxelsPerAxis;
	float scale;
	float posX;
	float posY;
	float posZ;
	float noiseOffsetX;
	float noiseOffsetY;
	float noiseOffsetZ;
}
params;

layout(set = 0, binding = 2, std430) coherent buffer Counter
{
	uint counter;
};

layout(set = 0, binding = 3, std430) restrict buffer LutBuffer
{
	int data[];
}
lut;
// psrdnoise (c) Stefan Gustavson and Ian McEwan,
// ver. 2021-12-02, published under the MIT license:
// https://github.com/stegu/psrdnoise/

// vec4 permute(vec4 i) {
//      vec4 im = mod(i, 289.0);
//      return mod(((im*34.0)+10.0)*im, 289.0);
// }

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


vec4 evaluate(vec3 coord)
{   
    vec3 grad;

    vec3 chunkPos = vec3(params.posX, params.posY, params.posZ);
    vec3 noiseOffset = vec3(params.noiseOffsetX, params.noiseOffsetY, params.noiseOffsetZ);


    // float worldVoxelSize = params.scale / params.numVoxelsPerAxis;
    vec3 percentPos = (coord / vec3(params.numVoxelsPerAxis)) - vec3(0.5); // 0 - 1 position relative to chunk
    // vec3 worldPos = (percentPos * params.scale) + chunkPos;
    // vec3 samplePos = ((worldPos + noiseOffset) / params.scale) * params.noiseScale;
    vec3 worldPos = (percentPos * params.scale);
    vec3 samplePos = (percentPos + (chunkPos / params.scale)) * params.noiseScale + noiseOffset;
	// float cellSize = 1.0 / params.numVoxelsPerAxis * params.scale;
	// float cx = int(params.posX / cellSize + 0.5 * sign(params.posX)) * cellSize;
	// float cy = int(params.posY / cellSize + 0.5 * sign(params.posY)) * cellSize;
	// float cz = int(params.posZ / cellSize + 0.5 * sign(params.posZ)) * cellSize;
// 	vec3 centreSnapped = vec3(params.posX, params.posY, params.posZ);// / params.scale;//vec3(cx, cy, cz);

// 	vec3 posNorm = (coord / vec3(params.numVoxelsPerAxis)) - vec3(0.5); // This is basically from -1 to 1 where this pos is

//     // vec3 worldPos = (posNorm * params.scale) + centreSnapped;
    
// 	vec3 noiseOffset = vec3(params.noiseOffsetX, params.noiseOffsetY, params.noiseOffsetZ); // World space noise offset
// 	// vec3 samplePos = (worldPos + noiseOffset) * params.noiseScale / params.scale; // Add the offset, then scale to sample space
//    // / params.scale;
//     vec3 worldPos = (posNorm * params.scale) + centreSnapped;
//     vec3 samplePos = (worldPos + noiseOffset)* params.noiseScale / params.scale;


	float sum = 0;
	float amplitude = 1;
	float weight = 1;
	
	for (int i = 0; i < 6; i ++)
	{
		float noise = psrdnoise(samplePos, vec3(0.0), 0.0, grad) * 2 - 1;
		noise = 1 - abs(noise);
		noise *= noise;
		noise *= weight;
		weight = max(0, min(1, noise * 10));
		sum += noise * amplitude;
		samplePos *= 2;
		amplitude *= 0.5;
	}
	float density = sum;
	density = -worldPos.y - density;//-(worldPos.y+100)/300 + density;

    // return vec4(worldPos, sin(worldPos.x));//psrdnoise(coord, vec3(0.0), 0.0, grad));
	return vec4(worldPos, density);
}

vec4 interpolateVerts(vec4 v1, vec4 v2, float isoLevel)
{
	//return (v1 + v2) * 0.5;
	float t = (isoLevel - v1.w) / (v2.w - v1.w);
	return v1 + t * (v2 - v1);
}

// int indexFromCoord(vec4 coord) {
//     float numVoxelsPerAxis = params.numVoxelsPerAxis;
// 	coord = coord - vec4(params.posX, params.posY, params.posZ, 0);
// 	return int((coord.z * numVoxelsPerAxis * numVoxelsPerAxis) + (coord.y * numVoxelsPerAxis) + coord.x);
// }

// int getDoubleID(vec4 coordA, vec4 coordB){
//     int voxCount = int(round(params.numVoxelsPerAxis));
//     int indexA = indexFromCoord(coordA);
//     int indexB = indexFromCoord(coordB);
//     return (indexA) + ((voxCount * voxCount * voxCount) * indexA);
// }


layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;
void main()
{
	vec3 id = gl_GlobalInvocationID;

	// 8 corners of the current cube
	vec4 cubeCorners[8] = {
		evaluate(vec3(id.x + 0, id.y + 0, id.z + 0)),
		evaluate(vec3(id.x + 1, id.y + 0, id.z + 0)),
		evaluate(vec3(id.x + 1, id.y + 0, id.z + 1)),
		evaluate(vec3(id.x + 0, id.y + 0, id.z + 1)),
		evaluate(vec3(id.x + 0, id.y + 1, id.z + 0)),
		evaluate(vec3(id.x + 1, id.y + 1, id.z + 0)),
		evaluate(vec3(id.x + 1, id.y + 1, id.z + 1)),
		evaluate(vec3(id.x + 0, id.y + 1, id.z + 1))
	};

	// Calculate unique index for each cube configuration.
	// There are 256 possible values
	// A value of 0 means cube is entirely inside surface; 255 entirely outside.
	// The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
	uint cubeIndex = 0;
	float isoLevel = params.isoLevel;
	if (cubeCorners[0].w < isoLevel) cubeIndex |= 1;
	if (cubeCorners[1].w < isoLevel) cubeIndex |= 2;
	if (cubeCorners[2].w < isoLevel) cubeIndex |= 4;
	if (cubeCorners[3].w < isoLevel) cubeIndex |= 8;
	if (cubeCorners[4].w < isoLevel) cubeIndex |= 16;
	if (cubeCorners[5].w < isoLevel) cubeIndex |= 32;
	if (cubeCorners[6].w < isoLevel) cubeIndex |= 64;
	if (cubeCorners[7].w < isoLevel) cubeIndex |= 128;

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
		currTri.a = interpolateVerts(cubeCorners[a0], cubeCorners[b0], isoLevel);
		currTri.b = interpolateVerts(cubeCorners[a1], cubeCorners[b1], isoLevel);
		currTri.c = interpolateVerts(cubeCorners[a2], cubeCorners[b2], isoLevel);

		vec3 ab = currTri.b.xyz - currTri.a.xyz;
		vec3 ac = currTri.c.xyz - currTri.a.xyz;
		currTri.norm = -vec4(normalize(cross(ab,ac)), 0);

		uint index = atomicAdd(counter, 1u);
		triangleBuffer.data[index] = currTri;
		
	}
}