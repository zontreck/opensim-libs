///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
 *	OPCODE - Optimized Collision Detection
 *	Copyright (C) 2001 Pierre Terdiman
 *	Homepage: http://www.codercorner.com/Opcode.htm
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Contains common classes & defs used in OPCODE.
 *	\file		OPC_Common.h
 *	\author		Pierre Terdiman
 *	\date		March, 20, 2001
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Include Guard
#ifndef __OPC_COMMON_H__
#define __OPC_COMMON_H__

#if defined(__AVX__)
ODE_PURE_INLINE  bool avxBoxesOverlap(const dReal* acenter, const dReal* bcenter, const dReal* aext, const dReal* bext)
{
    const __m128 sign = _mm_set1_ps(-0.0f);
    __m128 ma, mb, mc;

    ma = _mm_loadu_ps(acenter);
    mb = _mm_loadu_ps(bcenter);
    ma = _mm_sub_ps(ma, mb);

    ma = _mm_andnot_ps(sign, ma);

    mb = _mm_loadu_ps(aext);
    mc = _mm_loadu_ps(bext);
    mb = _mm_add_ps(mb, mc);

    ma = _mm_cmpgt_ps(ma, mb);
    return ((_mm_movemask_ps(ma) & 0x07) == 0);
}
#endif

class OPCODE_API CollisionAABB
{
public:
    //! Constructor
    inline_ CollisionAABB() {}
    //! Constructor
    inline_ CollisionAABB(const AABB& b) { b.GetCenter(mCenter);	b.GetExtents(mExtents); }
    //! Destructor
    inline_ ~CollisionAABB() {}

    //! Get min point of the box
    inline_ void GetMin(Point& min) const { min = mCenter - mExtents; }
    //! Get max point of the box
    inline_	void GetMax(Point& max) const { max = mCenter + mExtents; }

    //! Get component of the box's min point along a given axis
    inline_ float GetMin(udword axis) const { return mCenter[axis] - mExtents[axis]; }
    //! Get component of the box's max point along a given axis
    inline_ float GetMax(udword axis) const { return mCenter[axis] + mExtents[axis]; }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /**
     *	Setups an AABB from min & max vectors.
     *	\param		min			[in] the min point
     *	\param		max			[in] the max point
     */
     ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    inline_ void SetMinMax(const Point& min, const Point& max)
    {
        mCenter = (max + min)*0.5f;
        mExtents = (max - min)*0.5f;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /**
     *	Checks a box is inside another box.
     *	\param		box		[in] the other box
     *	\return		true if current box is inside input box
     */
     ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    inline_ BOOL IsInside(const CollisionAABB& box) const
    {
#if defined (__AVX__)
        return avxBoxesOverlap(&(box.mCenter.x), &(mCenter.x), &(box.mExtents.x), &(mExtents.x));
#else
        if (box.GetMin(0) > GetMin(0))	return FALSE;
        if (box.GetMin(1) > GetMin(1))	return FALSE;
        if (box.GetMin(2) > GetMin(2))	return FALSE;
        if (box.GetMax(0) < GetMax(0))	return FALSE;
        if (box.GetMax(1) < GetMax(1))	return FALSE;
        if (box.GetMax(2) < GetMax(2))	return FALSE;
        return TRUE;
#endif
    }

    Point mCenter; //!< Box center
    Point mExtents; //!< Box extents
};

//! Quickly rotates & translates a vector
inline_ void TransformPoint(Point& dest, const Point& source, const Matrix3x3& rot, const Point& trans)
{
    dest.x = trans.x + source.x * rot.m[0][0] + source.y * rot.m[1][0] + source.z * rot.m[2][0];
    dest.y = trans.y + source.x * rot.m[0][1] + source.y * rot.m[1][1] + source.z * rot.m[2][1];
    dest.z = trans.z + source.x * rot.m[0][2] + source.y * rot.m[1][2] + source.z * rot.m[2][2];
}

#endif //__OPC_COMMON_H__
