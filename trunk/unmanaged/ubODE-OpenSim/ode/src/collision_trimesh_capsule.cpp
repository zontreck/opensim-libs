/*************************************************************************
 *                                                                       *
 * Open Dynamics Engine, Copyright (C) 2001-2003 Russell L. Smith.       *
 * All rights reserved.  Email: russ@q12.org   Web: www.q12.org          *
 *                                                                       *
 * This library is free software; you can redistribute it and/or         *
 * modify it under the terms of EITHER:                                  *
 *   (1) The GNU Lesser General Public License as published by the Free  *
 *       Software Foundation; either version 2.1 of the License, or (at  *
 *       your option) any later version. The text of the GNU Lesser      *
 *       General Public License is included with this library in the     *
 *       file LICENSE.TXT.                                               *
 *   (2) The BSD-style license that is included with this library in     *
 *       the file LICENSE-BSD.TXT.                                       *
 *                                                                       *
 * This library is distributed in the hope that it will be useful,       *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the files    *
 * LICENSE.TXT and LICENSE-BSD.TXT for more details.                     *
 *                                                                       *
 *************************************************************************/

/*
 *	Triangle-Capsule(Capsule) collider by Alen Ladavac
 *  Ported to ODE by Nguyen Binh
 */

// NOTES from Nguyen Binh
//	14 Apr : Seem to be robust
//       There is a problem when you use original Step and set contact friction
//		surface.mu = dInfinity;
//		More description : 
//			When I dropped Capsule over the bunny ears, it seems to stuck
//			there for a while. I think the cause is when you set surface.mu = dInfinity;
//			the friction force is too high so it just hang the capsule there.
//			So the good cure for this is to set mu = around 1.5 (in my case)
//		For StepFast1, this become as solid as rock : StepFast1 just approximate 
//		friction force.

// NOTES from Croteam's Alen
//As a side note... there are some extra contacts that can be generated
//on the edge between two triangles, and if the capsule penetrates deeply into
//the triangle (usually happens with large mass or low FPS), some such
//contacts can in some cases push the capsule away from the edge instead of
//away from the two triangles. This shows up as capsule slowing down a bit
//when hitting an edge while sliding along a flat tesselated grid of
//triangles. This is only if capsule is standing upwards.

//Same thing can appear whenever a smooth object (e.g sphere) hits such an
//edge, and it needs to be solved as a special case probably. This is a
//problem we are looking forward to address soon.

#include <ode/collision.h>
#include <ode/rotation.h>
#include "config.h"
#include "matrix.h"
#include "odemath.h"
#include "collision_util.h"
#include "collision_std.h"
#include "collision_trimesh_internal.h"
#include "util.h"


// OPCODE version

// largest number, double or float
#if defined(dSINGLE)
#define MAX_REAL	FLT_MAX
#define MIN_REAL	(-FLT_MAX)
#else
#define MAX_REAL	DBL_MAX
#define MIN_REAL	(-DBL_MAX)
#endif

// To optimize before send contacts to dynamic part
#define OPTIMIZE_CONTACTS 1

/// Local contacts data
typedef struct _sLocalContactData
{
    dVector3	vPos;
    dVector3	vNormal;
    dReal		fDepth;
    int			triIndex;
    int			nFlags; // 0 = filtered out, 1 = OK
}sLocalContactData;

struct sTrimeshCapsuleColliderData
{
    sTrimeshCapsuleColliderData(): m_gLocalContacts(NULL), m_ctContacts(0) { memset(m_vN, 0, sizeof(dVector3)); }

    void SetupInitialContext(dxTriMesh *TriMesh, dxGeom *Capsule, int flags, int skip);
    int TestCollisionForSingleTriangle(int ctContacts0, int Triint, dVector3 dv[3], 
        uint8 flags, bool &bOutFinishSearching, bool singleSide);

#if OPTIMIZE_CONTACTS
    void _OptimizeLocalContacts();
#endif
    int	_ProcessLocalContacts(dContactGeom *contact, dxTriMesh *TriMesh, dxGeom *Capsule);

    static BOOL _cldClipEdgeToPlaneNormNoOffset(dVector3 &vEpnt0, dVector3 &vEpnt1, const dVector3 plPlaneNormal);
    static BOOL _cldClipEdgeToPlaneNorm(dVector3 &vEpnt0, dVector3 &vEpnt1, const dVector3 plPlaneNormal, const dReal PlaneOffset);
    BOOL _cldTestNormal(const dVector3 vAxis, const int iAxis);
    BOOL _cldTestAxis(const dVector3 vAxis, int iAxis);
    BOOL _cldTestSeparatingAxesOfCapsule(const dVector3 &v0, const dVector3 &v1, 
        const dVector3 &v2, uint8 flags);
    void _cldTestOneTriangleVSCapsule(const dVector3 &v0, const dVector3 &v1, 
        const dVector3 &v2, uint8 flags, bool singleside);

    sLocalContactData   *m_gLocalContacts;
    unsigned int		m_ctContacts;

    // capsule data
    // real time data
    dMatrix3  m_mCapsuleRotation;
    dVector3   m_vCapsulePosition;
    dVector3   m_vCapsuleAxis;
    dVector3   m_vSizeOnAxis;
    // static data
    dReal      m_fCapsuleRadius;
    dReal      m_fCapCilinderSize;
    dReal      m_fCapsuleSize;
    // mesh data
    // dMatrix4  mHullDstPl;
    dMatrix3   m_mTriMeshRot;
    dVector3   m_mTriMeshPos;
    dVector3   m_vE0, m_vE1, m_vE2;

    // global collider data
    dVector3 m_vNormal;
    dReal    m_fBestDepth;
    dReal    m_fBestCenterrt;
    int		m_iBestAxis;
    dVector3 m_vN;

    dVector3 m_vV0; 
    dVector3 m_vV1;
    dVector3 m_vV2;

    // ODE contact's specific
    unsigned int m_iFlags;
    int m_iStride;
};

// Capsule lie on axis number 3 = (Z axis)
static const int nCAPSULE_AXIS = 2;


#if OPTIMIZE_CONTACTS

// Use to classify contacts to be "near" in position
static const dReal fSameContactPositionEpsilon = REAL(0.0001); // 1e-4
// Use to classify contacts to be "near" in normal direction
static const dReal fSameContactNormalEpsilon = REAL(0.0001); // 1e-4

// If this two contact can be classified as "near"
inline int _IsNearContacts(sLocalContactData& c1,sLocalContactData& c2)
{
    int bPosNear = 0;
    int bSameDir = 0;
    dVector3	vDiff;

    // First check if they are "near" in position
    dSubtractVectors3r4(vDiff, c1.vPos, c2.vPos);
    if (  (dFabs(vDiff[0]) < fSameContactPositionEpsilon)
        &&(dFabs(vDiff[1]) < fSameContactPositionEpsilon)
        &&(dFabs(vDiff[2]) < fSameContactPositionEpsilon))
    {
        bPosNear = 1;
    }

    // Second check if they are "near" in normal direction
    dSubtractVectors3r4(vDiff, c1.vNormal, c2.vNormal);
    if (  (dFabs(vDiff[0]) < fSameContactNormalEpsilon)
        &&(dFabs(vDiff[1]) < fSameContactNormalEpsilon)
        &&(dFabs(vDiff[2]) < fSameContactNormalEpsilon) )
    {
        bSameDir = 1;
    }

    // Will be "near" if position and normal direction are "near"
    return (bPosNear && bSameDir);
}

inline int _IsBetter(sLocalContactData& c1,sLocalContactData& c2)
{
    // The not better will be throw away
    // You can change the selection criteria here
    return (c1.fDepth > c2.fDepth);
}

// iterate through gLocalContacts and filtered out "near contact"
void sTrimeshCapsuleColliderData::_OptimizeLocalContacts()
{
    int nContacts = m_ctContacts;

    for (int i = 0; i < nContacts-1; i++)
    {
        for (int j = i+1; j < nContacts; j++)
        {
            if (_IsNearContacts(m_gLocalContacts[i],m_gLocalContacts[j]))
            {
                // If they are seem to be the samed then filtered 
                // out the least penetrate one
                if (_IsBetter(m_gLocalContacts[j],m_gLocalContacts[i]))
                {
                    m_gLocalContacts[i].nFlags = 0; // filtered 1st contact
                }
                else
                {
                    m_gLocalContacts[j].nFlags = 0; // filtered 2nd contact
                }

                // NOTE
                // There is other way is to add two depth together but
                // it not work so well. Why???
            }
        }
    }
}
#endif // OPTIMIZE_CONTACTS

int	sTrimeshCapsuleColliderData::_ProcessLocalContacts(dContactGeom *contact,
                                                       dxTriMesh *TriMesh, dxGeom *Capsule)
{
#if OPTIMIZE_CONTACTS
    if (m_ctContacts > 1 && !(m_iFlags & CONTACTS_UNIMPORTANT))
    {
        // Can be optimized...
        _OptimizeLocalContacts();
    }
#endif		

    unsigned int iContact = 0;
    dContactGeom* Contact = 0;

    unsigned int nFinalContact = 0;

    for (iContact = 0; iContact < m_ctContacts; iContact ++)
    {
        // Ensure that we haven't created too many contacts
        if( nFinalContact >= (m_iFlags & NUMC_MASK)) 
        {
            break;
        }

        if (1 == m_gLocalContacts[iContact].nFlags)
        {
            Contact =  SAFECONTACT(m_iFlags, contact, nFinalContact, m_iStride);
            Contact->depth = m_gLocalContacts[iContact].fDepth;
            dCopyVector3r4(Contact->normal, m_gLocalContacts[iContact].vNormal);
            dCopyVector3r4(Contact->pos ,m_gLocalContacts[iContact].vPos);
            Contact->g1 = TriMesh;
            Contact->g2 = Capsule;
            Contact->side1 = m_gLocalContacts[iContact].triIndex;
            Contact->side2 = -1;

            nFinalContact++;
        }
    }
    // debug
    //if (nFinalContact != m_ctContacts)
    //{
    //	printf("[Info] %d contacts generated,%d  filtered.\n",m_ctContacts,m_ctContacts-nFinalContact);
    //}

    return nFinalContact;
}


BOOL sTrimeshCapsuleColliderData::_cldClipEdgeToPlaneNorm(
    dVector3 &vEpnt0, dVector3 &vEpnt1, const dVector3 plPlane, const dReal offset)
{
    // calculate distance of edge points to plane
    dReal fDistance0 = dCalcVectorDot3(vEpnt0, plPlane) + offset;
    dReal fDistance1 = dCalcVectorDot3(vEpnt1, plPlane) + offset;
    // if both points are behind the plane
    if (fDistance0 < dEpsilon && fDistance1 < dEpsilon)
        return FALSE;

    // if both points in front of the plane
    if (fDistance0 >= 0 && fDistance1 >= 0)
        return TRUE;

    // find intersection point of edge and plane
    dReal factor = fDistance0 / (fDistance0 - fDistance1);
    // clamp correct edge to intersection point
    if (fDistance0 < 0)
    {
        dCalcLerpVectors3r4(vEpnt0, vEpnt0, vEpnt1, factor);
    }
    else
    {
        dCalcLerpVectors3r4(vEpnt1, vEpnt0, vEpnt1, factor);
    }
    return TRUE;
}

BOOL sTrimeshCapsuleColliderData::_cldClipEdgeToPlaneNormNoOffset(
    dVector3 &vEpnt0, dVector3 &vEpnt1, const dVector3 plPlane)
{
    // calculate distance of edge points to plane
    dReal fDistance0 = dCalcVectorDot3(vEpnt0, plPlane);
    dReal fDistance1 = dCalcVectorDot3(vEpnt1, plPlane);

    // if both points are behind the plane
    if (fDistance0 < dEpsilon && fDistance1 < dEpsilon)
        return FALSE;

    // if both points in front of the plane
    if (fDistance0 >= 0 && fDistance1 >= 0)
        return TRUE;

    // find intersection point of edge and plane
    dReal factor = fDistance0 / (fDistance0 - fDistance1);
    // clamp correct edge to intersection point
    if (fDistance0 < 0)
    {
        dCalcLerpVectors3r4(vEpnt0, vEpnt0, vEpnt1, factor);
    }
    else
    {
        dCalcLerpVectors3r4(vEpnt1, vEpnt0, vEpnt1, factor);
    }
    return TRUE;
}

BOOL sTrimeshCapsuleColliderData::_cldTestAxis(const dVector3 vAxis, int iAxis)
{
    dReal min = dCalcVectorDot3(m_vV0, vAxis);
    dReal max = dCalcVectorDot3(m_vV1, vAxis);
    dReal tmp = dCalcVectorDot3(m_vV2, vAxis);
    if (min > max)
    {
        dReal tmp2 = max < tmp ? max : tmp;
        max = min > tmp ? min : tmp;
        min = tmp2;
    }
    else
    {
        if (tmp < min)
            min = tmp;
        if (tmp > max)
            max = tmp;
    }

    // find triangle's center of interval on axis
    dReal fCenter = (min + max) * REAL(0.5);
    // calculate triangles half interval 
    dReal fTriangleRadius = max - fCenter;

    // project capsule on vAxis
    dReal frc = dFabs(dCalcVectorDot3(m_vSizeOnAxis, vAxis)) + m_fCapsuleRadius;

    // calculate depth 
    dReal frcPlusTRadius = frc + fTriangleRadius;
    dReal fDepth = dFabs(fCenter) - frcPlusTRadius;

    // if they do not overlap, 
    if (fDepth > 0)
    {
        // exit, we have no intersection
        return FALSE;
    }

    // if greater then best found so far
    if (fDepth * dReal(1.5) > m_fBestDepth)
    {
        // remember depth
        m_fBestDepth = fDepth;
        m_iBestAxis = iAxis;

        // flip normal if interval is wrong faced
        if (fCenter < 0)
        {
            dCopyNegatedVector3r4(m_vNormal, vAxis);
            m_fBestCenterrt = -fCenter - fTriangleRadius;
        }
        else
        {
            dCopyVector3r4(m_vNormal, vAxis);
            m_fBestCenterrt = fCenter - fTriangleRadius;
        }
    }

    return TRUE;
}

BOOL sTrimeshCapsuleColliderData::_cldTestNormal(const dVector3 vAxis,const  int iAxis)
{
    dReal min = dCalcVectorDot3(m_vV0, vAxis);
    dReal max = dCalcVectorDot3(m_vV1, vAxis);
    dReal tmp = dCalcVectorDot3(m_vV2, vAxis);
    if (min > max)
    {
        dReal tmp2 = max < tmp ? max : tmp;
        max = min > tmp ? min : tmp;
        min = tmp2;
    }
    else
    {
        if (tmp < min)
            min = tmp;
        if (tmp > max)
            max = tmp;
    }

    // find triangle's center of interval on axis
    dReal fCenter = (min + max) * REAL(0.5);
    // calculate triangles half interval 
    dReal fTriangleRadius = max - fCenter;

    // project capsule on vAxis
    dReal frc = dFabs(dCalcVectorDot3(m_vSizeOnAxis, vAxis)) + m_fCapsuleRadius;

    // calculate depth 
    dReal frcPlusTRadius = frc + fTriangleRadius;
    dReal fDepth = dFabs(fCenter) - frcPlusTRadius;

    // if they do not overlap, 
    if (fDepth > 0)
    {
        // exit, we have no intersection
        return FALSE;
    }

    //always first remember depth
    m_fBestDepth = fDepth;
    m_iBestAxis = iAxis;

    dCopyVector3r4(m_vNormal, vAxis);
    m_fBestCenterrt = fCenter - fTriangleRadius;

    return TRUE;
}

// helper for less key strokes
inline void _CalculateAxis(const dVector3& v1, const dVector3& v2, const dVector3& v3, const dVector3& v4, dVector3& r)
{
    dVector3 t1;
    dVector3 t2;

    dSubtractVectors3r4(t1, v1, v2);
    dCalcVectorCross3r4(t2, t1, v3);
    dCalcVectorCross3r4(r, t2, v4);
}

BOOL sTrimeshCapsuleColliderData::_cldTestSeparatingAxesOfCapsule(
    const dVector3 &v0, const dVector3 &v1, const dVector3 &v2, uint8 flags) 
{
    // Translate triangle to Cc cord.
    // used in _cldTestAxis
    dSubtractVectors3r4(m_vV0, v0, m_vCapsulePosition);
    dSubtractVectors3r4(m_vV1, v1, m_vCapsulePosition);
    dSubtractVectors3r4(m_vV2, v2, m_vCapsulePosition);

    // reset best axis
    m_iBestAxis = 0;
    // reset best depth
    m_fBestDepth  = MIN_REAL;
    // reset separating axis vector
    dVector3 vAxis;

    // We begin to test for 19 separating axis now
    // I wonder does it help if we employ the method like ISA-GJK???
    // Or at least we should do experiment and find what axis will
    // be most likely to be separating axis to check it first.


    // Original
    // axis m_vN
    //vAxis = -m_vN;
    dCopyNegatedVector3r4(vAxis, m_vN);
    if (!_cldTestNormal(vAxis, 1)) 
    { 
        return FALSE; 
    }

    if (flags == 0)
        return TRUE;

    dVector3 vCp0;
    dVector3 vCp1;

    dAddVectors3r4(vCp0, m_vCapsulePosition, m_vSizeOnAxis);
    dSubtractVectors3r4(vCp1, m_vCapsulePosition, m_vSizeOnAxis);

    if (flags & dxTriMeshData::kEdge0)
    {
        // axis CxE0 - Edge 0
        dCalcVectorCross3r4(vAxis, m_vCapsuleAxis, m_vE0);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 2))
                return FALSE;
        }

        // second capsule point
        // axis ((Cp1-V0) x E0) x E0
        _CalculateAxis(vCp1, v0, m_vE0, m_vE0, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 8))
                return FALSE;
        }

        // first capsule point
        // axis ((Cp0-V0) x E0) x E0
        _CalculateAxis(vCp0, v0, m_vE0, m_vE0, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 5))
                return FALSE;
        }
    }

    if (flags & dxTriMeshData::kEdge1)
    {
        // axis CxE1 - Edge 1
        dCalcVectorCross3r4(vAxis, m_vCapsuleAxis, m_vE1);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 3))
                return FALSE;
        }

        // axis ((Cp0-V1) x E1) x E1
        _CalculateAxis(vCp0, v1, m_vE1, m_vE1, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 6))
                return FALSE;
        }

        // axis ((Cp1-V1) x E1) x E1
        _CalculateAxis(vCp1, v1, m_vE1, m_vE1, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 9))
                return FALSE;
        }
    }

    if (flags & dxTriMeshData::kEdge2)
    {
        // axis CxE2 - Edge 2
        dCalcVectorCross3r4(vAxis, m_vCapsuleAxis, m_vE2);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 4))
                return FALSE;
        }

        // axis ((Cp0-V2) x E2) x E2
        _CalculateAxis(vCp0, v2, m_vE2, m_vE2, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 7))
                return FALSE;
        }

        // axis ((Cp1-V2) x E2) x E2
        _CalculateAxis(vCp1, v2, m_vE2, m_vE2, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 10))
                return FALSE;
        }
    }

    if (flags & dxTriMeshData::kVert0)
    {
        // first vertex on triangle
        // axis ((V0-Cp0) x C) x C
        _CalculateAxis(v0, vCp0, m_vCapsuleAxis, m_vCapsuleAxis, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 11))
                return FALSE;
        }

        // first triangle vertex and first capsule point
        //vAxis = v0 - vCp0;
        dSubtractVectors3r4(vAxis, v0, vCp0);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 14))
                return FALSE;
        }

        // first triangle vertex and second capsule point
        //vAxis = v0 - vCp1;
        dSubtractVectors3r4(vAxis, v0, vCp1);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 17))
                return FALSE;
        }
    }

    if (flags & dxTriMeshData::kVert1)
    {
        // second vertex on triangle
        // axis ((V1-Cp0) x C) x C
        _CalculateAxis(v1, vCp0, m_vCapsuleAxis, m_vCapsuleAxis, vAxis);	
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 12))
                return FALSE;
        }

        // second triangle vertex and first capsule point
        //vAxis = v1 - vCp0;
        dSubtractVectors3r4(vAxis, v1, vCp0);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 15))
                return FALSE;
        }

        // second triangle vertex and second capsule point
        //vAxis = v1 - vCp1;
        dSubtractVectors3r4(vAxis, v1, vCp1);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 18))
                return FALSE;
        }
    }

    if (flags & dxTriMeshData::kVert2)
    {
        // third vertex on triangle
        // axis ((V2-Cp0) x C) x C
        _CalculateAxis(v2, vCp0, m_vCapsuleAxis, m_vCapsuleAxis, vAxis);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 13))
                return FALSE;
        }

        // third triangle vertex and first capsule point
        //vAxis = v2 - vCp0;
        dSubtractVectors3r4(vAxis, v2, vCp0);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 16))
                return FALSE;
        }

        // third triangle vertex and second capsule point
        //vAxis = v2 - vCp1;
        dSubtractVectors3r4(vAxis, v2, vCp1);
        if (dSafeNormalize3fast(vAxis))
        {
            if (!_cldTestAxis(vAxis, 19))
                return FALSE;
        }
    }
    return TRUE;
}

// test one mesh triangle on intersection with capsule
void sTrimeshCapsuleColliderData::_cldTestOneTriangleVSCapsule(
    const dVector3 &v0, const dVector3 &v1, const dVector3 &v2,
    uint8 flags, bool singleSide)
{
    // calculate edges
    dSubtractVectors3r4(m_vE0, v1, v0);
    dSubtractVectors3r4(m_vE1, v2, v1);
    dSubtractVectors3r4(m_vE2, v0, v2);

    // calculate poly normal (negative)
    dCalcVectorCross3r4(m_vN, m_vE0, m_vE1);

    // Even though all triangles might be initially valid, 
    // a triangle may degenerate into a segment after applying 
    // space transformation.
    if (!dSafeNormalize3(m_vN))
    {
        return;
    }

    // calculate capsule distance to plane
    dReal fDistanceCapsuleCenterToPlane = dCalcVectorDot3(m_vCapsulePosition, m_vN) - dCalcVectorDot3(v0, m_vN);

    // Capsule must be over positive side of triangle
    if (fDistanceCapsuleCenterToPlane < 0  && singleSide) 
    {
        // if not don't generate contacts
        return;
    }

    if (fDistanceCapsuleCenterToPlane > m_fCapsuleSize)
        return;

    dVector3 vPnt0;
    dVector3 vPnt1;
    dVector3 vPnt2;

    if (fDistanceCapsuleCenterToPlane < 0 )
    {
        if (fDistanceCapsuleCenterToPlane < -m_fCapsuleSize)
            return;

        dCopyVector3r4(vPnt0, v0);
        dCopyVector3r4(vPnt1, v2);
        dCopyVector3r4(vPnt2, v1);
    
        dCopyNegatedVector3r4(m_vN, m_vN);

        dSubtractVectors3(m_vE0, v2, v0);
        dSubtractVectors3(m_vE1, v1, v2);
        dSubtractVectors3(m_vE2, v0, v1);
        flags = dxTriMeshData::kUseAll;
    }
    else
    {
        dCopyVector3r4(vPnt0,v0);
        dCopyVector3r4(vPnt1,v1);
        dCopyVector3r4(vPnt2,v2);
    }

    // do intersection test and find best separating axis
    if (!_cldTestSeparatingAxesOfCapsule(vPnt0, vPnt1, vPnt2, flags))
    {
        // if not found do nothing
        return;
    }

    // if best separation axis is not found
    if (m_iBestAxis == 0 ) 
    {
        // this should not happen (we should already exit in that case)
        dIASSERT(FALSE);
        // do nothing
        return;
    }

    // calculate caps centers in absolute space
    dVector3 vCposTrans;
    dCopyVector3r4(vCposTrans, m_vCapsulePosition);
    dAddScaledVector3r4(vCposTrans, m_vNormal, m_fCapsuleRadius);

    // transform capsule edge points into triangle space
    dVector3 vCEdgePoint0;
    dAddVectors3r4(vCEdgePoint0, vCposTrans, m_vSizeOnAxis);
    dSubtractVectors3r4(vCEdgePoint0, vPnt0);

    dVector3 vCEdgePoint1;
    dSubtractVectors3(vCEdgePoint1, vCposTrans, m_vSizeOnAxis);
    dSubtractVectors3r4(vCEdgePoint1, vPnt0);

    dVector3 _minus_vN;
    dCopyNegatedVector3r4(_minus_vN, m_vN);

    if (!_cldClipEdgeToPlaneNormNoOffset( vCEdgePoint0, vCEdgePoint1, _minus_vN))
    { 
        return; 
    }

    // plane edge 0
    dVector3 vTemp;
    dCalcVectorCross3r4(vTemp, m_vN, m_vE0);
    if (!_cldClipEdgeToPlaneNormNoOffset( vCEdgePoint0, vCEdgePoint1, vTemp))
    { 
        return; 
    }
    // plane with edge 1
    dCalcVectorCross3r4(vTemp, m_vN, m_vE1);
    if (!_cldClipEdgeToPlaneNorm( vCEdgePoint0, vCEdgePoint1, vTemp, -(dCalcVectorDot3(m_vE0, vTemp))))
    { 
        return; 
    }
    // plane with edge 2
    dCalcVectorCross3r4(vTemp,m_vN,m_vE2);
    if (!_cldClipEdgeToPlaneNormNoOffset( vCEdgePoint0, vCEdgePoint1, vTemp))
    {
        return; 
    }

    // calculate depths for both contact points
    dAddVector3r4(vCEdgePoint0, vPnt0);
    dSubtractVectors3r4(vTemp, vCEdgePoint0, m_vCapsulePosition);
    dReal fDepth0 = dCalcVectorDot3(vTemp, m_vNormal) - m_fBestCenterrt;

    dAddVector3r4(vCEdgePoint1, vPnt0);
    dSubtractVectors3r4(vTemp, vCEdgePoint1, m_vCapsulePosition);
    dReal fDepth1 = dCalcVectorDot3(vTemp, m_vNormal) - m_fBestCenterrt;

    // clamp depths to zero
    if (fDepth0 < 0) 
        fDepth0 = 0.0f;

    if (fDepth1 < 0 ) 
        fDepth1 = 0.0f;

    // Cached contacts's data
    // contact 0

    m_gLocalContacts[m_ctContacts].fDepth = fDepth0;
    dCopyVector3r4(m_gLocalContacts[m_ctContacts].vNormal, m_vNormal);
    dCopyVector3r4(m_gLocalContacts[m_ctContacts].vPos, vCEdgePoint0);
    m_gLocalContacts[m_ctContacts].nFlags = 1;
    m_ctContacts++;

    if (m_ctContacts < (m_iFlags & NUMC_MASK))
    {
        // contact 1
        m_gLocalContacts[m_ctContacts].fDepth = fDepth1;
        dCopyVector3r4(m_gLocalContacts[m_ctContacts].vNormal, m_vNormal);
        dCopyVector3r4(m_gLocalContacts[m_ctContacts].vPos, vCEdgePoint1);
        m_gLocalContacts[m_ctContacts].nFlags = 1;
        m_ctContacts++;
    }
}

void sTrimeshCapsuleColliderData::SetupInitialContext(dxTriMesh *TriMesh, dxGeom *Capsule, 
                                                      int flags, int skip)
{
    dxPosR *CapsulePosR = Capsule->GetRecomputePosR();
    memcpy(m_mCapsuleRotation, CapsulePosR->R, sizeof(dMatrix3));
    memcpy(m_vCapsulePosition, CapsulePosR->pos, sizeof(dVector3));
 
    m_vCapsuleAxis[0] = m_mCapsuleRotation[nCAPSULE_AXIS];
    m_vCapsuleAxis[1] = m_mCapsuleRotation[4 + nCAPSULE_AXIS];
    m_vCapsuleAxis[2] = m_mCapsuleRotation[8 + nCAPSULE_AXIS];

    // Get size of Capsule
    m_fCapCilinderSize = ((dxCapsule*)Capsule)->halfLenZ;
    m_fCapsuleRadius = ((dxCapsule*)Capsule)->radius;
    m_fCapsuleSize = m_fCapCilinderSize + m_fCapsuleRadius;

    dCopyScaledVector3r4(m_vSizeOnAxis, m_vCapsuleAxis, m_fCapCilinderSize);

    dxPosR *meshPosR = TriMesh->GetRecomputePosR();
    memcpy(m_mTriMeshRot, meshPosR->R, sizeof(dMatrix3));
    memcpy(m_mTriMeshPos, meshPosR->pos, sizeof(dVector3));

    // global info for contact creation
    m_iStride =skip;
    m_iFlags = flags;

    // reset contact counter
    m_ctContacts = 0;	
}

int sTrimeshCapsuleColliderData::TestCollisionForSingleTriangle(int ctContacts0, 
         int Triint, dVector3 dv[3], uint8 flags, bool &bOutFinishSearching, bool singleSide)
{
    // test this triangle
    _cldTestOneTriangleVSCapsule(dv[0],dv[1],dv[2], flags, singleSide);

    // fill-in tri index for generated contacts
    for (; ctContacts0 < (int)m_ctContacts; ctContacts0++)
        m_gLocalContacts[ctContacts0].triIndex = Triint;

    // Putting "break" at the end of loop prevents unnecessary checks on first pass and "continue"
    bOutFinishSearching = (m_ctContacts >= (m_iFlags & NUMC_MASK));

    return ctContacts0;
}


static void dQueryCCTLPotentialCollisionTriangles(OBBCollider &Collider,
    const sTrimeshCapsuleColliderData &cData, dxTriMesh *TriMesh, dxGeom *Capsule,
    OBBCache &BoxCache)
{
    const float *capPtr = (float*)&cData.m_mCapsuleRotation[0];
    Matrix3x3 obbRot;
    obbRot.m[0][0] = capPtr[0];
    obbRot.m[1][0] = capPtr[1];
    obbRot.m[2][0] = capPtr[2];

    obbRot.m[0][1] = capPtr[4];
    obbRot.m[1][1] = capPtr[5];
    obbRot.m[2][1] = capPtr[6];

    obbRot.m[0][2] = capPtr[8];
    obbRot.m[1][2] = capPtr[9];
    obbRot.m[2][2] = capPtr[10];

    Point cCenter(cData.m_vCapsulePosition[0], cData.m_vCapsulePosition[1], cData.m_vCapsulePosition[2]);
    Point cExtents(cData.m_fCapsuleRadius, cData.m_fCapsuleRadius, cData.m_fCapsuleSize);
    OBB obbCapsule(cCenter, cExtents, obbRot);
    
    Matrix4x4 MeshMatrix;
    MakeMatrix(cData.m_mTriMeshPos, cData.m_mTriMeshRot, MeshMatrix);

    // TC results
    if (TriMesh->doBoxTC)
    {
        dxTriMesh::BoxTC* BoxTC = 0;
        for (int i = 0; i < TriMesh->BoxTCCache.size(); i++)
        {
            if (TriMesh->BoxTCCache[i].Geom == Capsule)
            {
                BoxTC = &TriMesh->BoxTCCache[i];
                break;
            }
        }
        if (!BoxTC)
        {
            TriMesh->BoxTCCache.push(dxTriMesh::BoxTC());

            BoxTC = &TriMesh->BoxTCCache[TriMesh->BoxTCCache.size() - 1];
            BoxTC->Geom = Capsule;
            BoxTC->FatCoeff = 1.0f;
        }

        // Intersect
        Collider.SetTemporalCoherence(true);
        Collider.Collide(*BoxTC, obbCapsule, TriMesh->Data->BVTree, null, &MeshMatrix);
    }
    else
    {
        Collider.SetTemporalCoherence(false);
        Collider.Collide(BoxCache, obbCapsule, TriMesh->Data->BVTree, null, &MeshMatrix) ;
    }
}

// capsule - trimesh by CroTeam
// Ported by Nguyem Binh
int dCollideCCTL(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip)
{
    dIASSERT (skip >= (int)sizeof(dContactGeom));
    dIASSERT (o1->type == dTriMeshClass);
    dIASSERT (o2->type == dCapsuleClass);
    dIASSERT ((flags & NUMC_MASK) >= 1);

    int nContactCount = 0;

    dxTriMesh *TriMesh = (dxTriMesh*)o1;
    dxGeom *Capsule = o2;

    sTrimeshCapsuleColliderData cData;
    cData.SetupInitialContext(TriMesh, Capsule, flags, skip);

    const unsigned uiTLSKind = TriMesh->getParentSpaceTLSKind();
    dIASSERT(uiTLSKind == Capsule->getParentSpaceTLSKind()); // The colliding spaces must use matching cleanup method
    TrimeshCollidersCache *pccColliderCache = GetTrimeshCollidersCache(uiTLSKind);
    OBBCollider& Collider = pccColliderCache->_OBBCollider;

    // Will it better to use LSS here? -> confirm Pierre.
    dQueryCCTLPotentialCollisionTriangles(Collider, cData, 
        TriMesh, Capsule, pccColliderCache->defaultBoxCache);

    if (Collider.GetContactStatus()) 
    {
        // Retrieve data
        int TriCount = Collider.GetNbTouchedPrimitives();
        if (TriCount != 0)
        {
            bool singleSide = true;
            uint8 meshflags = TriMesh->Data->meshFlags;

            if ((meshflags & dxTriMeshData::closedSurface) == 0)
            {
                dReal size = REAL(1.5) * cData.m_fCapsuleRadius;
                dVector3& ext = TriMesh->Data->AABBExtents;
                if (size < ext[0])
                    singleSide = false;
                else if (size < ext[1])
                    singleSide = false;
                else if (size < ext[2])
                    singleSide = false;
            }

            const int* Triangles = (const int*)Collider.GetTouchedPrimitives();

            // allocate buffer for local contacts on stack
            cData.m_gLocalContacts = (sLocalContactData*)dALLOCA16(sizeof(sLocalContactData)*(cData.m_iFlags & NUMC_MASK));

            unsigned int ctContacts0 = cData.m_ctContacts;

            uint8* UseFlags = TriMesh->Data->UseFlags;

            // loop through all intersecting triangles
            if (UseFlags)
            {
                for (int i = 0; i < TriCount; i++)
                {
                    const int Triint = Triangles[i];

                    dVector3 dv[3];
                    FetchTriangle(TriMesh, Triint, cData.m_mTriMeshPos, cData.m_mTriMeshRot, dv);

                    bool bFinishSearching;
                    ctContacts0 = cData.TestCollisionForSingleTriangle(ctContacts0, Triint, dv, UseFlags[Triint], bFinishSearching, singleSide);

                    if (bFinishSearching)
                    {
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < TriCount; i++)
                {
                    const int Triint = Triangles[i];
                    dVector3 dv[3];
                    FetchTriangle(TriMesh, Triint, cData.m_mTriMeshPos, cData.m_mTriMeshRot, dv);

                    bool bFinishSearching;
                    ctContacts0 = cData.TestCollisionForSingleTriangle(ctContacts0, Triint, dv, (uint8)dxTriMeshData::kUseAll, bFinishSearching, singleSide);

                    if (bFinishSearching)
                    {
                        break;
                    }
                }
            }

            if (cData.m_ctContacts != 0)
            {
                nContactCount = cData._ProcessLocalContacts(contact, TriMesh, Capsule);
            }
        }
    }

    return nContactCount;
}
