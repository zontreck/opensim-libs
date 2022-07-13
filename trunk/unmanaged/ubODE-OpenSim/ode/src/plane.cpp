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

standard ODE geometry primitives: public API and pairwise collision functions.

the rule is that only the low level primitive collision functions should set
dContactGeom::g1 and dContactGeom::g2.

*/

#include <ode/common.h>
#include <ode/collision.h>
#include <ode/rotation.h>
#include "config.h"
#include "matrix.h"
#include "odemath.h"
#include "collision_kernel.h"
#include "collision_std.h"
#include "collision_util.h"

#ifdef _MSC_VER
#pragma warning(disable:4291)  // for VC++, no complaints about "no matching operator delete found"
#endif

//****************************************************************************
// plane public API

static void make_sure_plane_normal_has_unit_length (dxPlane *g)
{
    dSafeNormalize3(g->p);
}


dxPlane::dxPlane (dSpaceID space, dReal a, dReal b, dReal c, dReal d) :
dxGeom (space,0)
{
    type = dPlaneClass;
    p[0] = a;
    p[1] = b;
    p[2] = c;
    p[3] = dSafeNormalize3(p) ? d : 0;
}

void dxPlane::computeAABB()
{
    // Planes that have normal vectors aligned along an axis can use a
    // less comprehensive (half space) bounding box.
    if (p[2] == 0)
    {
        if (p[1] == 0.0f)
        {
            // normal aligned with x-axis
            if (p[0] > 0)
            {
                aabb[0] = -dInfinity;
                aabb[1] = p[3];
            }
            else
            {
                aabb[0] = -p[3];
                aabb[1] = dInfinity;
            }
            aabb[2] = -dInfinity;
            aabb[3] = dInfinity;
            aabb[4] = -dInfinity;
            aabb[5] = dInfinity;
            return;
        }
        if (p[0] == 0.0f)
        {
            // normal aligned with y-axis
            aabb[0] = -dInfinity;
            aabb[1] = dInfinity;
            if(p[1] > 0)
            {
                aabb[2] = -dInfinity;
                aabb[3] = p[3];
            }
            else
            {
                aabb[2] = -p[3];
                aabb[3] = dInfinity;
            }
            aabb[4] = -dInfinity;
            aabb[5] = dInfinity;
            return;
        }
    }
    if ( p[0] == 0.0f && p[1] == 0.0f )
    {
        // normal aligned with z-axis
        aabb[0] = -dInfinity;
        aabb[1] = dInfinity;
        aabb[2] = -dInfinity;
        aabb[3] = dInfinity;
        if (p[2] > 0)
        {
            aabb[4] = -dInfinity;
            aabb[5] = p[3];
        }
        else
        {
            aabb[4] = -p[3];
            aabb[5] = dInfinity;
        }
        return;
    }

    aabb[0] = -dInfinity;
    aabb[1] = dInfinity;
    aabb[2] = -dInfinity;
    aabb[3] = dInfinity;
    aabb[4] = -dInfinity;
    aabb[5] = dInfinity;

}

dGeomID dCreatePlane (dSpaceID space, dReal a, dReal b, dReal c, dReal d)
{
    return new dxPlane (space, a, b, c, d);
}

void dGeomPlaneSetParams (dGeomID g, dReal a, dReal b, dReal c, dReal d)
{
    dUASSERT (g && g->type == dPlaneClass,"argument not a plane");
    ((dxPlane*)g)->p[0] = a;
    ((dxPlane*)g)->p[1] = b;
    ((dxPlane*)g)->p[2] = c;
    ((dxPlane*)g)->p[3] = dSafeNormalize3(((dxPlane*)g)->p) ? d : 0;
    dGeomMoved (g);
}

void dGeomPlaneGetParams (dGeomID g, dVector4 result)
{
    dUASSERT (g && g->type == dPlaneClass,"argument not a plane");
    dCopyVector4(result, ((dxPlane*)g)->p);
}

dReal dGeomPlanePointDepth (dGeomID g, dReal x, dReal y, dReal z)
{
    dUASSERT (g && g->type == dPlaneClass,"argument not a plane");
    dVector3& p = ((dxPlane*)g)->p;
    return p[3] - p[0] * x - p[1] * y - p[2] * z;
}
