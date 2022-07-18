#define LOCAL_EPSILON 0.000001f

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Computes a ray-triangle intersection test.
 *	Original code from Tomas Möller's "Fast Minimum Storage Ray-Triangle Intersection".
 *	It's been optimized a bit with integer code, and modified to return a non-intersection if distance from
 *	ray origin to triangle is negative.
 *
 *	\param		vert0	[in] triangle vertex
 *	\param		vert1	[in] triangle vertex
 *	\param		vert2	[in] triangle vertex
 *	\return		true on overlap. mStabbedFace is filled with relevant info.
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
inline_ BOOL RayCollider::RayTriOverlap(const Point& vert0, const Point& vert1, const Point& vert2)
{
    // Stats
    mNbRayPrimTests++;

    // Find vectors for two edges sharing vert0
    Point edge1 = vert1 - vert0;
    Point edge2 = vert2 - vert0;

    // Begin calculating determinant - also used to calculate U parameter
    Point pvec = mDir ^ edge2;

    // If determinant is near zero, ray lies in plane of triangle
    float det = edge1 | pvec;

    if (mCulling)
    {
        if (det <= LOCAL_EPSILON * FCMin2(edge1.SquareMagnitude(), edge2.SquareMagnitude()))
            return FALSE;

    }
    else
    {
        // the non-culling branch
        if (fabsf(det) <= LOCAL_EPSILON * FCMin2(edge1.SquareMagnitude(), edge2.SquareMagnitude()))
            return FALSE;
    }

    float OneOverDet = 1.0f / det;

    // Calculate distance from vert0 to ray origin
    Point tvec = mOrigin - vert0;

    // Calculate U parameter and test bounds
    mStabbedFace.mU = (tvec | pvec) * OneOverDet;
    //		if(IR(u)&0x80000000 || u>1.0f)					return FALSE;
    if (mStabbedFace.mU < 0 || mStabbedFace.mU > 1.0f)
        return FALSE;

    // prepare to test V parameter
    Point qvec = tvec ^ edge1;

    // Calculate V parameter and test bounds
    mStabbedFace.mV = (mDir | qvec) * OneOverDet;
    if (mStabbedFace.mV < 0 || mStabbedFace.mU + mStabbedFace.mV > 1.0f)
        return FALSE;

    // Calculate t, ray intersects triangle
    mStabbedFace.mDistance = (edge2 | qvec) * OneOverDet;
    // Intersection point is valid if distance is positive (else it can just be a face behind the orig point)
    if (mStabbedFace.mDistance < 0)
        return FALSE;

    return TRUE;
}
