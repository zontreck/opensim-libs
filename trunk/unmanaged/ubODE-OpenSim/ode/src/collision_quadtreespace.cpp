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

// QuadTreeSpace by Erwin de Vries.
// With math corrections by Oleh Derevenko. ;)

#include <ode/common.h>
#include <ode/collision_space.h>
#include <ode/collision.h>
#include "config.h"
#include "matrix.h"
#include "collision_kernel.h"

#include "collision_space_internal.h"

#define GEOM_ENABLED(g) (((g)->gflags & GEOM_ENABLE_TEST_MASK) == GEOM_ENABLE_TEST_VALUE)

class Block
{
public:
    dReal mMinX, mMaxX;
    dReal mMinZ, mMaxZ;

    dGeomID mFirst;
    int mGeomCount;

    Block* mParent;
    Block* mChildren;

    void Create(const dReal MinX, const dReal MaxX, const dReal MinZ, const dReal MaxZ, Block* Parent, int Depth, Block*& Blocks);

    void Collide(void* UserData, dNearCallback* Callback);
    void Collide(dGeomID g1, dGeomID g2, void* UserData, dNearCallback* Callback);
    void CollideLocal(dGeomID g2, void* UserData, dNearCallback* Callback);

    void AddObject(dGeomID Object);
    void DelObject(dGeomID Object);
    void Traverse(dGeomID Object);

    bool Inside(const dReal* AABB);

    Block* GetBlock(const dReal* AABB);
    Block* GetBlockChild(const dReal* AABB);
};

void Block::Create(const dReal MinX, const dReal MaxX, const dReal MinY, const dReal MaxY, Block* Parent, int Depth, Block* &Blocks)
{
    dIASSERT(MinX <= MaxX);
    dIASSERT(MinY <= MaxY);

    mGeomCount = 0;
    mFirst = 0;

    mMinX = MinX;
    mMaxX = MaxX;

    mMinZ = MinY;
    mMaxZ = MaxY;

    this->mParent = Parent;

    if (Depth > 0)
    {
        const int ChildDepth = Depth - 1;

        mChildren = Blocks;
        Blocks += 4;

        const dReal ChildExtentX = (MaxX - MinX) * dReal(0.5);
        const dReal ChildExtentY = (MaxY - MinY) * dReal(0.5);
        const dReal ChildMidX = MinX + ChildExtentX;
        const dReal ChildMidY = MinY + ChildExtentY;
        mChildren[0].Create(MinX, ChildMidX, MinY, ChildMidY, this, ChildDepth, Blocks);
        mChildren[1].Create(MinX, ChildMidX, ChildMidY, MaxY, this, ChildDepth, Blocks);

        mChildren[2].Create(ChildMidX, MaxX, MinY, ChildMidY, this, ChildDepth, Blocks);
        mChildren[3].Create(ChildMidX, MaxX, ChildMidY, MaxY, this, ChildDepth, Blocks);
    }
    else
        mChildren = 0;
}

void Block::Collide(void* UserData, dNearCallback* Callback)
{
    // Collide the local list
    dxGeom* g = mFirst;
    while (g)
    {
        if (GEOM_ENABLED(g))
        {
            Collide(g, g->next_ex, UserData, Callback);
        }
        g = g->next_ex;
    }

    // Recurse for children
    if (mChildren)
    {
        for (int i = 0; i < 4; i++)
        {
            Block &CurrentChild = mChildren[i];
            if (CurrentChild.mGeomCount <= 1)
            {	// Early out
                continue;
            }
            CurrentChild.Collide(UserData, Callback);
        }
    }
}

// Note: g2 is assumed to be in this Block
void Block::Collide(dGeomID g1, dGeomID g2, void* UserData, dNearCallback* Callback)
{
    // Collide against local list
    while (g2)
    {
        if (GEOM_ENABLED(g2) && testCollideAABBs(g1, g2))
            Callback(UserData, g1, g2);
        g2 = g2->next_ex;
    }

    // Collide against children
    if (mChildren)
    {
        for (int i = 0; i < 4; i++)
        {
            Block &CurrentChild = mChildren[i];
            // Early out for empty blocks
            if (CurrentChild.mGeomCount == 0)
            {
                continue;
            }

            // Does the geom's AABB collide with the block?
            // Don't do AABB tests for single geom blocks.
            if (CurrentChild.mGeomCount > 1)
            {
                if (g1->aabb[0] > CurrentChild.mMaxX ||
                    g1->aabb[1] < CurrentChild.mMinX ||
                    g1->aabb[2] > CurrentChild.mMaxZ ||
                    g1->aabb[3] < CurrentChild.mMinZ)
                    continue;
            }
            CurrentChild.Collide(g1, CurrentChild.mFirst, UserData, Callback);
        }
    }
}
void Block::CollideLocal(dGeomID g2, void* UserData, dNearCallback* Callback)
{
    // Collide against local list
    dxGeom* g1 = mFirst;
    while (g1)
    {
        if (GEOM_ENABLED(g1) && testCollideAABBs(g1, g2))
            Callback(UserData, g1, g2);
        g1 = g1->next_ex;
    }
}

void Block::AddObject(dGeomID Object)
{
    // Add the geom
    Object->next_ex = mFirst;
    mFirst = Object;
    Object->tome_ex = (dxGeom**)this;

    // Now traverse upwards to tell that we have a geom
    Block* Block = this;
    do
    {
        Block->mGeomCount++;
        Block = Block->mParent;
    }
    while (Block);
}

void Block::DelObject(dGeomID Object)
{
    // Del the geom
    dxGeom* g = mFirst;
    dxGeom* Last = 0;
    while (g)
    {
        if (g == Object)
        {
            if (Last)
                Last->next_ex = g->next_ex;
            else
                mFirst = g->next_ex;
            break;
        }
        Last = g;
        g = g->next_ex;
    }

    Object->tome_ex = 0;

    // Now traverse upwards to tell that we have lost a geom
    Block* Block = this;
    do
    {
        Block->mGeomCount--;
        Block = Block->mParent;
    }
    while (Block);
}

void Block::Traverse(dGeomID Object)
{
    Block* NewBlock = GetBlock(Object->aabb);

    if (NewBlock != this)
    {
        // Remove the geom from the old block and add it to the new block.
        // This could be more optimal, but the loss should be very small.
        DelObject(Object);
        NewBlock->AddObject(Object);
    }
}

inline bool Block::Inside(const dReal* AABB)
{
    return AABB[0] > mMinX &&
           AABB[1] < mMaxX &&
           AABB[2] > mMinZ &&
           AABB[3] < mMaxZ;
}

Block* Block::GetBlock(const dReal* AABB)
{
    return Inside(AABB) ? GetBlockChild(AABB) : ( mParent ? mParent->GetBlock(AABB) : this );
}

Block* Block::GetBlockChild(const dReal* AABB)
{
    if (mChildren)
    {
        for (int i = 0; i < 4; i++)
        {
            Block &CurrentChild = mChildren[i];
            if (CurrentChild.Inside(AABB))
            {
                return CurrentChild.GetBlockChild(AABB);	// Child will have good block
            }
        }
    }
    return this;	// This is the best block
}

//****************************************************************************
// quadtree space

struct dxQuadTreeSpace : public dxSpace
{
    size_t BlockCount;
    Block* Blocks;	// Blocks[0] is the root

    dArray<dxGeom*> DirtyList;

    dxQuadTreeSpace(dSpaceID _space, const dVector3 Center, const dVector3 Extents, int Depth);
    ~dxQuadTreeSpace();

    dxGeom* getGeom(int i);

    void add(dxGeom* g);
    void remove(dxGeom* g);
    void dirty(dxGeom* g);

    void computeAABB();

    void cleanGeoms();
    void collide(void* UserData, dNearCallback* Callback);
    void collide2(void* UserData, dxGeom* g1, dNearCallback* Callback);

};

namespace {
    inline size_t numNodes(int depth) 
    {
        // A 4-ary tree has (4^(depth+1) - 1)/3 nodes
        // Note: split up into multiple constant expressions for readability
        const int k = depth + 1;
        const size_t fourToNthPlusOne = (size_t)1 << (2 * k); // 4^k = 2^(2k)
        return (fourToNthPlusOne - 1) / 3;
    }
}

dxQuadTreeSpace::dxQuadTreeSpace(dSpaceID _space, const dVector3 Center, const dVector3 Extents, int Depth) : dxSpace(_space)
{
    type = dQuadTreeSpaceClass;
    BlockCount = numNodes(Depth);
    if (BlockCount <= 0)
        return;

    Blocks = (Block*)dAlloc(BlockCount * sizeof(Block));
    if (!Blocks)
    {
        BlockCount = 0;
        return;
    }

    dReal MinX = Center[0] - Extents[0];
    dReal MaxX = dNextAfter((Center[0] + Extents[0]), (dReal)dInfinity);
    dReal MinZ = Center[1] - Extents[1];
    dReal MaxZ = dNextAfter((Center[1] + Extents[1]), (dReal)dInfinity);

    Block* nBlocks = this->Blocks + 1;	// This pointer gets modified!
    this->Blocks[0].Create(MinX, MaxX, MinZ, MaxZ, 0, Depth, nBlocks);

    // Init AABB. We initialize to infinity because it is not illegal for an object to be outside of the tree. Its simply inserted in the root block
    aabb[0] = -dInfinity;
    aabb[1] = dInfinity;
    aabb[2] = -dInfinity;
    aabb[3] = dInfinity;
    aabb[4] = -dInfinity;
    aabb[5] = dInfinity;
}

dxQuadTreeSpace::~dxQuadTreeSpace()
{
    if (Blocks && BlockCount > 0)
    {
        dFree(Blocks, BlockCount * sizeof(Block));
    }
}

dxGeom* dxQuadTreeSpace::getGeom(int Index){
    dUASSERT(Index >= 0 && Index < count, "index out of range");

    //@@@
    dDebug (0,"dxQuadTreeSpace::getGeom() not yet implemented");
    return 0;
    // This doesnt work
/*
    if (CurrentIndex == Index){
        // Loop through all objects in the local list
CHILDRECURSE:
        if (CurrentObject){
            dGeomID g = CurrentObject;
            CurrentObject = CurrentObject->next_ex;
            CurrentIndex++;

#ifdef DRAWBLOCKS
            DrawBlock(CurrentBlock);
#endif	//DRAWBLOCKS
            return g;
        }
        else{
            // Now lets loop through our children. Starting at index 0.
            if (CurrentBlock->Children){
                CurrentChild[CurrentLevel] = 0;
PARENTRECURSE:
                for (int& i = CurrentChild[CurrentLevel]; i < SPLITS; i++){
                    if (CurrentBlock->Children[i].GeomCount == 0){
                        continue;
                    }
                    CurrentBlock = &CurrentBlock->Children[i];
                    CurrentObject = CurrentBlock->First;

                    i++;

                    CurrentLevel++;
                    goto CHILDRECURSE;
                }
            }
        }

        // Now lets go back to the parent so it can continue processing its other children.
        if (CurrentBlock->Parent){
            CurrentBlock = CurrentBlock->Parent;
            CurrentLevel--;
            goto PARENTRECURSE;
        }
    }
    else{
        CurrentBlock = &Blocks[0];
        CurrentLevel = 0;
        CurrentObject = CurrentObject;
        CurrentIndex = 0;

        // Other states are already set
        CurrentObject = CurrentBlock->First;
    }


    if (current_geom && current_index == Index - 1){
        //current_geom = current_geom->next_ex; // next
        current_index = Index;
        return current_geom;
    }
    else for (int i = 0; i < Index; i++){	// this will be verrrrrrry slow
        getGeom(i);
    }
    return 0;
    */
}

void dxQuadTreeSpace::add(dxGeom* g)
{
    CHECK_NOT_LOCKED (this);
    dAASSERT(g);
    dUASSERT(g->tome_ex == 0 && g->next_ex == 0, "geom is already in a space");

    DirtyList.push(g);
    Blocks[0].GetBlock(g->aabb)->AddObject(g);	// Add to best block

    dxSpace::add(g);
}

void dxQuadTreeSpace::remove(dxGeom* g){
    CHECK_NOT_LOCKED(this);
    dAASSERT(g);
    dUASSERT(g->parent_space == this,"object is not in this space");

    // remove
    ((Block*)g->tome_ex)->DelObject(g);

    for (int i = 0; i < DirtyList.size(); i++)
    {
        if (DirtyList[i] == g)
        {
            DirtyList.remove(i);
            // (mg) there can be multiple instances of a dirty object on stack  be sure to remove ALL and not just first, for this we decrement i
            --i;
        }
    }
    dxSpace::remove(g);
}

void dxQuadTreeSpace::dirty(dxGeom* g)
{
    DirtyList.push(g);
}

void dxQuadTreeSpace::computeAABB()
{
    //
}

void dxQuadTreeSpace::cleanGeoms()
{
    // compute the AABBs of all dirty geoms, and clear the dirty flags
    lock_count++;

    for (int i = 0; i < DirtyList.size(); i++)
    {
        dxGeom* g = DirtyList[i];
        if (IS_SPACE(g))
        {
            ((dxSpace*)g)->cleanGeoms();
        }
        g->recomputeAABB();
        g->gflags &= (~(GEOM_DIRTY|GEOM_AABB_BAD));

        ((Block*)g->tome_ex)->Traverse(g);
    }
    DirtyList.setSize(0);

    lock_count--;
}

void dxQuadTreeSpace::collide(void* UserData, dNearCallback* Callback)
{
    dAASSERT(Callback);

    lock_count++;
    cleanGeoms();

    Blocks[0].Collide(UserData, Callback);

    lock_count--;
}


struct DataCallback
{
    void *data;
    dNearCallback *callback;
};
// Invokes the callback with arguments swapped
static void swap_callback(void *data, dxGeom *g1, dxGeom *g2)
{
    DataCallback *dc = (DataCallback*)data;
    dc->callback(dc->data, g2, g1);
}


void dxQuadTreeSpace::collide2(void* UserData, dxGeom* g2, dNearCallback* Callback)
{
    dAASSERT(g2 && Callback);

    lock_count++;
    cleanGeoms();
    g2->recomputeAABB();

    if (g2->parent_space == this)
    {
        // The block the geom is in
        Block* CurrentBlock = (Block*)g2->tome_ex;

        // Collide against block and its children
        DataCallback dc = {UserData, Callback};
        CurrentBlock->Collide(g2, CurrentBlock->mFirst, &dc, swap_callback);

        // Collide against parents
        while ((CurrentBlock = CurrentBlock->mParent))
            CurrentBlock->CollideLocal(g2, UserData, Callback);
    }
    else
    {
        DataCallback dc = {UserData, Callback};
        Blocks[0].Collide(g2, Blocks[0].mFirst, &dc, swap_callback);
    }

    lock_count--;
}

dSpaceID dQuadTreeSpaceCreate(dxSpace* space, const dVector3 Center, const dVector3 Extents, int Depth)
{
    return new dxQuadTreeSpace(space, Center, Extents, Depth);
}
