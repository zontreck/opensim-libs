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

// TriMesh code by Erwin de Vries.

#include <ode/collision.h>
#include <ode/rotation.h>
#include "config.h"
#include "matrix.h"
#include "odemath.h"

#include "collision_util.h"
#include "collision_trimesh_internal.h"

int dCollideRTL(dxGeom* TriGeom, dxGeom* RayGeom, int Flags, dContactGeom* Contacts, int Stride){
    dIASSERT (Stride >= (int)sizeof(dContactGeom));
    dIASSERT (TriGeom->type == dTriMeshClass);
    dIASSERT (RayGeom->type == dRayClass);
    dIASSERT ((Flags & NUMC_MASK) >= 1);

    dxPosR* dpr = TriGeom->GetRecomputePosR();
    const dMatrix3& TLRotation = *(const dMatrix3*)dpr->R;
    const dVector3& TLPosition = *(const dVector3*)dpr->pos;

    dxTriMesh* TriMesh = (dxTriMesh*)TriGeom;
    const unsigned uiTLSKind = TriMesh->getParentSpaceTLSKind();
    dIASSERT(uiTLSKind == RayGeom->getParentSpaceTLSKind()); // The colliding spaces must use matching cleanup method
    TrimeshCollidersCache *pccColliderCache = GetTrimeshCollidersCache(uiTLSKind);
    RayCollider& Collider = pccColliderCache->_RayCollider;

    dReal Length = dGeomRayGetLength(RayGeom);

    int FirstContact = dGeomRayGetFirstContact(RayGeom);
    int BackfaceCull = dGeomRayGetBackfaceCull(RayGeom);
    int ClosestHit = dGeomRayGetClosestHit(RayGeom);

    Collider.SetFirstContact(FirstContact != 0);
    Collider.SetClosestHit(ClosestHit != 0);
    Collider.SetCulling(BackfaceCull != 0);
    Collider.SetMaxDist(Length);

    dVector3 Origin, Direction;
    dGeomRayGet(RayGeom, Origin, Direction);

    /* Make Ray */
    Ray WorldRay;
    WorldRay.mOrig.x = Origin[0];
    WorldRay.mOrig.y = Origin[1];
    WorldRay.mOrig.z = Origin[2];
    WorldRay.mDir.x = Direction[0];
    WorldRay.mDir.y = Direction[1];
    WorldRay.mDir.z = Direction[2];

    /* Intersect */
    int TriCount = 0;
    Matrix4x4 amatrix;
    if (Collider.Collide(WorldRay, TriMesh->Data->BVTree, &MakeMatrix(TLPosition, TLRotation, amatrix)))
    {
        TriCount = pccColliderCache->Faces.GetNbFaces();
    }

    if (TriCount == 0)
    {
        return 0;
    }

    const CollisionFace* Faces = pccColliderCache->Faces.GetFaces();
    CollisionFace face;

    int OutTriCount = 0;
    int TriIndex;
    dVector3 dv[3];

    if (TriMesh->RayCallback == null)
    {
        for (int i = 0; i < TriCount; i++)
        {
            face = Faces[i];
            TriIndex = face.mFaceID;

            //if (!Callback(TriMesh, RayGeom, TriIndex))
            //    continue;

            dContactGeom* Contact = SAFECONTACT(Flags, Contacts, OutTriCount, Stride);

            FetchTriangle(TriMesh, TriIndex, TLPosition, TLRotation, dv);
            dSubtractVectors3r4(dv[1], dv[0]);
            dSubtractVectors3r4(dv[2], dv[0]);
            dCalcVectorCross3r4(Contact->normal, dv[2], dv[1]);	// Reversed

            // Even though all triangles might be initially valid, 
            // a triangle may degenerate into a segment after applying 
            // space transformation.
            if (dSafeNormalize3(Contact->normal))
            {
                dReal T = face.mDistance;
                dAddScaledVector3(Contact->pos, Origin, Direction, T);

                Contact->depth = T;
                Contact->g1 = TriGeom;
                Contact->g2 = RayGeom;
                Contact->side1 = TriIndex;
                Contact->side2 = -1;

                OutTriCount++;

                // Putting "break" at the end of loop prevents unnecessary checks on first pass and "continue"
                if (OutTriCount >= (Flags & NUMC_MASK))
                    break;
            }
        }
    }
    else
    {
        for (int i = 0; i < TriCount; i++)
        {
            face = Faces[i];
            TriIndex = face.mFaceID;
            if (TriMesh->RayCallback(TriMesh, RayGeom, TriIndex, face.mU, face.mV))
            {
                //if (!Callback(TriMesh, RayGeom, TriIndex))
                //    continue;

                dContactGeom* Contact = SAFECONTACT(Flags, Contacts, OutTriCount, Stride);

                FetchTriangle(TriMesh, TriIndex, TLPosition, TLRotation, dv);

                dSubtractVectors3r4(dv[1], dv[0]);
                dSubtractVectors3r4(dv[2], dv[0]);
                dCalcVectorCross3r4(Contact->normal, dv[2], dv[1]);	// Reversed

                // Even though all triangles might be initially valid, 
                // a triangle may degenerate into a segment after applying 
                // space transformation.
                if (dSafeNormalize3(Contact->normal))
                {
                    dReal T = face.mDistance;
                    dAddScaledVector3(Contact->pos, Origin, Direction, T);

                    Contact->depth = T;
                    Contact->g1 = TriGeom;
                    Contact->g2 = RayGeom;
                    Contact->side1 = TriIndex;
                    Contact->side2 = -1;

                    OutTriCount++;

                    // Putting "break" at the end of loop prevents unnecessary checks on first pass and "continue"
                    if (OutTriCount >= (Flags & NUMC_MASK))
                        break;
                }
            }
        }
    }
    return OutTriCount;
}

