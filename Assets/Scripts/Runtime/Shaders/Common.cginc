// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx


struct Point {
    float3 position;
    uint color;
};

uint PcxEncodeColorAlpha(uint4 rgba)
{
    return rgba.x | (rgba.y << 8) | (rgba.z << 16) | (rgba.w << 24);
}

uint4 PcxDecodeColorAlpha(uint data)
{
    uint r = (data      ) & 0xff;
    uint g = (data >>  8) & 0xff;
    uint b = (data >> 16) & 0xff;
    uint a = (data >> 24) & 0xff;
    return uint4(r, g, b, a);
}

half4 PcxDecodeColorAlphaFinal(uint data)
{
    half r = (half) ((data      ) & 0xff) / 255;
    half g = (half) ((data >>  8) & 0xff) / 255;
    half b = (half) ((data >> 16) & 0xff) / 255;
    half a = (half) ((data >> 24) & 0xff) / 255;
    return half4(r, g, b, a);
}

half4 IncreaseLightAlpha( half4 col , float intensity)
{
    //intensity *= intensity * intensity;
    return half4( 255*pow(col.x/255.0,1/intensity), 255*pow(col.y/255.0,1/intensity), 255*pow(col.z/255.0,1/intensity), col.w );
}

Point  MakePoint(float3 p, uint c) 
{ 
    Point pt;
    pt.position = p;
    pt.color = c;
    return pt; 
}

struct CLOSEST
{
    float3 dir;
    float distance;
};

CLOSEST DistLinePoint(float3 a, float3 b, float3 p)   // makes a capsule between a and b
{   
   CLOSEST r;
   float3 ba = b-a;
   float3 pa = p-a;
   float d = dot(ba, pa);
   if (0 > d) r.dir = pa;
   else {
        float l = length(ba);
        if (d > pow(l,2)) r.dir = p - b;
        else r.dir = (pa) - d / l * normalize(b-a);
        }
   r.distance = length(r.dir);
   return r;
}

bool insidePrism(float3 A, float3 B, float3 C, float3 N, float3 P)
{     
    float3 u = dot( N, cross( P-B , P-C ) );
    float3 v = dot( N, cross( P-C , P-A ) );
    float3 w = dot( N, cross( P-A , P-B ) );
    return !(u < 0) & !(v < 0) & !(w < 0);
}

CLOSEST DistTrianglePoint(float3 a, float3 b, float3 c, float3 p)   // makes a capsule between a and b
{   
   float3 n = normalize( cross(b-a, c-a) );
   float distance = dot(n,p-a);

   float3 dir = distance * n;
 
    if (insidePrism(a,b,c,n,p)) {
        CLOSEST r;
        r.dir = dir;
        r.distance = distance; //abs(distance)
        return r;
    } else { 
             CLOSEST s = DistLinePoint(a, b, p);
             CLOSEST sTemp = DistLinePoint(b, c, p);
             if (sTemp.distance < s.distance) s = sTemp;
             sTemp = DistLinePoint(a, c, p);
             if (sTemp.distance < s.distance) s = sTemp;
             return s;
      }   
}

uint Hash(uint s)
{
    s ^= 2747636419u;
    s *= 2654435769u;
    s ^= s >> 16;
    s *= 2654435769u;
    s ^= s >> 16;
    s *= 2654435769u;
    return s;
}

float Random(uint seed)
{
    return float(Hash(seed)) / 4294967295.0; // 2^32-1
}

float3 Random3(uint seed)
{
	return float3(Random( (uint) (_Time[0]*(float) seed ) ) -0.5 , Random( (uint) (_Time[1]* (float) seed) ) , Random( (uint) (_Time[2]* (float) seed) ) -0.5);
}
float3 Random3Eq(uint seed)
{
	return float3(Random( (uint) (_Time[0]*(float) seed ) ) -0.5 , Random( (uint) (_Time[1]* (float) seed) ) -0.5, Random( (uint) (_Time[2]* (float) seed) ) -0.5);
}
