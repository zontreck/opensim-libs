///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
 *	OPCODE - Optimized Collision Detection
 *	Copyright (C) 2001 Pierre Terdiman
 *	Homepage: http://www.codercorner.com/Opcode.htm
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Contains code for optimized trees. Implements 2 trees:
 *	- normal
 *	- no leaf
 *
 *	\file		OPC_OptimizedTree.cpp
 *	\author		Pierre Terdiman
 *	\date		March, 20, 2001
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	A standard AABB tree.
 *
 *	\class		AABBCollisionTree
 *	\author		Pierre Terdiman
 *	\version	1.3
 *	\date		March, 20, 2001
*/
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	A no-leaf AABB tree.
 *
 *	\class		AABBNoLeafTree
 *	\author		Pierre Terdiman
 *	\version	1.3
 *	\date		March, 20, 2001
*/
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Precompiled Header
#include "Stdafx.h"

using namespace Opcode;

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Builds an implicit tree from a standard one. An implicit tree is a complete tree (2*N-1 nodes) whose negative
 *	box pointers and primitive pointers have been made implicit, hence packing 3 pointers in one.
 *
 *	Layout for implicit trees:
 *	Node:
 *			- box
 *			- data (32-bits value)
 *
 *	if data's LSB = 1 =>	remaining bits are a primitive pointer
 *	else					remaining bits are a P-node pointer, and N = P + 1
 *
 *	\relates	AABBCollisionNode
 *	\fn			_BuildCollisionTree(AABBCollisionNode* linear, const udword box_id, udword& current_id, const AABBTreeNode* current_node)
 *	\param		linear			[in] base address of destination nodes
 *	\param		box_id			[in] index of destination node
 *	\param		current_id		[in] current running index
 *	\param		current_node	[in] current node from input tree
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
static void _BuildCollisionTree(AABBCollisionNode* linear, const udword box_id, udword& current_id, const AABBTreeNode* current_node)
{
	// Current node from input tree is "current_node". Must be flattened into "linear[boxid]".

	// Store the AABB
	current_node->GetAABB()->GetCenter(linear[box_id].mAABB.mCenter);
	current_node->GetAABB()->GetExtents(linear[box_id].mAABB.mExtents);
	// Store remaining info
	if(current_node->IsLeaf())
	{
		// The input tree must be complete => i.e. one primitive/leaf
		ASSERT(current_node->GetNbPrimitives()==1);
		// Get the primitive index from the input tree
		udword PrimitiveIndex = current_node->GetPrimitives()[0];
		// Setup box data as the primitive index, marked as leaf
		linear[box_id].mData = (PrimitiveIndex<<1)|1;
	}
	else
	{
		// To make the negative one implicit, we must store P and N in successive order
		udword PosID = current_id++;	// Get a new id for positive child
		udword NegID = current_id++;	// Get a new id for negative child
		// Setup box data as the forthcoming new P pointer
		linear[box_id].mData = (size_t)&linear[PosID];
		// Make sure it's not marked as leaf
		ASSERT(!(linear[box_id].mData&1));
		// Recurse with new IDs
		_BuildCollisionTree(linear, PosID, current_id, current_node->GetPos());
		_BuildCollisionTree(linear, NegID, current_id, current_node->GetNeg());
	}
}
*/

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Builds a "no-leaf" tree from a standard one. This is a tree whose leaf nodes have been removed.
 *
 *	Layout for no-leaf trees:
 *
 *	Node:
 *			- box
 *			- P pointer => a node (LSB=0) or a primitive (LSB=1)
 *			- N pointer => a node (LSB=0) or a primitive (LSB=1)
 *
 *	\relates	AABBNoLeafNode
 *	\fn			_BuildNoLeafTree(AABBNoLeafNode* linear, const udword box_id, udword& current_id, const AABBTreeNode* current_node)
 *	\param		linear			[in] base address of destination nodes
 *	\param		box_id			[in] index of destination node
 *	\param		current_id		[in] current running index
 *	\param		current_node	[in] current node from input tree
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
static void _BuildNoLeafTree(AABBNoLeafNode* linear, const udword box_id, udword& current_id, const AABBTreeNode* current_node)
{
	const AABBTreeNode* P = current_node->GetPos();
	const AABBTreeNode* N = current_node->GetNeg();
	// Leaf nodes here?!
	ASSERT(P);
	ASSERT(N);
	// Internal node => keep the box
	current_node->GetAABB()->GetCenter(linear[box_id].mAABB.mCenter);
	current_node->GetAABB()->GetExtents(linear[box_id].mAABB.mExtents);

	if(P->IsLeaf())
	{
		// The input tree must be complete => i.e. one primitive/leaf
		ASSERT(P->GetNbPrimitives()==1);
		// Get the primitive index from the input tree
		udword PrimitiveIndex = P->GetPrimitives()[0];
		// Setup prev box data as the primitive index, marked as leaf
		linear[box_id].mPosData = (PrimitiveIndex<<1)|1;
	}
	else
	{
		// Get a new id for positive child
		udword PosID = current_id++;
		// Setup box data
		linear[box_id].mPosData = (size_t)&linear[PosID];
		// Make sure it's not marked as leaf
		ASSERT(!(linear[box_id].mPosData&1));
		// Recurse
		_BuildNoLeafTree(linear, PosID, current_id, P);
	}

	if(N->IsLeaf())
	{
		// The input tree must be complete => i.e. one primitive/leaf
		ASSERT(N->GetNbPrimitives()==1);
		// Get the primitive index from the input tree
		udword PrimitiveIndex = N->GetPrimitives()[0];
		// Setup prev box data as the primitive index, marked as leaf
		linear[box_id].mNegData = (PrimitiveIndex<<1)|1;
	}
	else
	{
		// Get a new id for negative child
		udword NegID = current_id++;
		// Setup box data
		linear[box_id].mNegData = (size_t)&linear[NegID];
		// Make sure it's not marked as leaf
		ASSERT(!(linear[box_id].mNegData&1));
		// Recurse
		_BuildNoLeafTree(linear, NegID, current_id, N);
	}
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Constructor.
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
AABBCollisionTree::AABBCollisionTree() : mNodes(null)
{
}
*/
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Destructor.
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
AABBCollisionTree::~AABBCollisionTree()
{
	DELETEARRAY(mNodes);
}
*/
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Builds the collision tree from a generic AABB tree.
 *	\param		tree			[in] generic AABB tree
 *	\return		true if success
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
bool AABBCollisionTree::Build(AABBTree* tree)
{
	// Checkings
	if(!tree)	return false;
	// Check the input tree is complete
	udword NbTriangles	= tree->GetNbPrimitives();
	udword NbNodes		= tree->GetNbNodes();
	if(NbNodes!=NbTriangles*2-1)	return false;

	// Get nodes
	if(mNbNodes!=NbNodes)	// Same number of nodes => keep moving
	{
		mNbNodes = NbNodes;
		DELETEARRAY(mNodes);
		mNodes = new AABBCollisionNode[mNbNodes];
		CHECKALLOC(mNodes);
	}

	// Build the tree
	udword CurID = 1;
	_BuildCollisionTree(mNodes, 0, CurID, tree);
	ASSERT(CurID==mNbNodes);

	return true;
}
*/
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Refits the collision tree after vertices have been modified.
 *	\param		mesh_interface	[in] mesh interface for current model
 *	\return		true if success
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
bool AABBCollisionTree::Refit(const MeshInterface* )
{
	ASSERT(!"Not implemented since AABBCollisionTrees have twice as more nodes to refit as AABBNoLeafTrees!");
	return false;
}
*/
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Walks the tree and call the user back for each node.
 *	\param		callback	[in] walking callback
 *	\param		user_data	[in] callback's user data
 *	\return		true if success
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
bool AABBCollisionTree::Walk(GenericWalkingCallback callback, void* user_data) const
{
	if(!callback)	return false;

	struct Local
	{
		static void _Walk(const AABBCollisionNode* current_node, GenericWalkingCallback callback, void* user_data)
		{
			if(!current_node || !(callback)(current_node, user_data))	return;

			if(!current_node->IsLeaf())
			{
				_Walk(current_node->GetPos(), callback, user_data);
				_Walk(current_node->GetNeg(), callback, user_data);
			}
		}
	};
	Local::_Walk(mNodes, callback, user_data);
	return true;
}
*/

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Constructor.
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
AABBNoLeafTree::AABBNoLeafTree() : mNodes(null)
{
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Destructor.
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
AABBNoLeafTree::~AABBNoLeafTree()
{
	DELETEARRAY(mNodes);
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Builds the collision tree from a generic AABB tree.
 *	\param		tree			[in] generic AABB tree
 *	\return		true if success
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
bool AABBNoLeafTree::Build(AABBTree* tree)
{
	// Checkings
	if(!tree)	return false;
	// Check the input tree is complete
	udword NbTriangles	= tree->GetNbPrimitives();
	udword NbNodes		= tree->GetNbNodes();
	if(NbNodes!=NbTriangles*2-1)	return false;

	// Get nodes
	if(mNbNodes!=NbTriangles-1)	// Same number of nodes => keep moving
	{
		mNbNodes = NbTriangles-1;
		DELETEARRAY(mNodes);
		mNodes = new AABBNoLeafNode[mNbNodes];
		CHECKALLOC(mNodes);
	}

	// Build the tree
	udword CurID = 1;
	_BuildNoLeafTree(mNodes, 0, CurID, tree);
	ASSERT(CurID==mNbNodes);

	return true;
}

inline_ void lComputeMinMax(Point& min, Point& max, const VertexPointers& vp)
{
    MinMax(min.x, max.x, vp.Vertex[0]->x, vp.Vertex[1]->x, vp.Vertex[2]->x);
    MinMax(min.y, max.y, vp.Vertex[0]->y, vp.Vertex[1]->y, vp.Vertex[2]->y);
    MinMax(min.z, max.z, vp.Vertex[0]->z, vp.Vertex[1]->z, vp.Vertex[2]->z);
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Refits the collision tree after vertices have been modified.
 *	\param		mesh_interface	[in] mesh interface for current model
 *	\return		true if success
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
bool AABBNoLeafTree::Refit(const MeshInterface* mesh_interface)
{
	// Checkings
	if(!mesh_interface)	return false;

	// Bottom-up update
	VertexPointers VP;
	Point Min,Max;
	Point Min_,Max_;
	udword Index = mNbNodes;
	while(Index--)
	{
		AABBNoLeafNode& Current = mNodes[Index];

		if(Current.HasPosLeaf())
		{
			mesh_interface->GetTriangle(VP, Current.GetPosPrimitive());
			lComputeMinMax(Min, Max, VP);
		}
		else
		{
			const CollisionAABB& CurrentBox = Current.GetPos()->mAABB;
			CurrentBox.GetMin(Min);
			CurrentBox.GetMax(Max);
		}

		if(Current.HasNegLeaf())
		{
			mesh_interface->GetTriangle(VP, Current.GetNegPrimitive());
			lComputeMinMax(Min_, Max_, VP);
		}
		else
		{
			const CollisionAABB& CurrentBox = Current.GetNeg()->mAABB;
			CurrentBox.GetMin(Min_);
			CurrentBox.GetMax(Max_);
		}

        if (Min_.x < Min.x)
            Min.x = Min_.x;
        if (Min_.y < Min.y)
            Min.y = Min_.y;
        if (Min_.z < Min.z)
            Min.z = Min_.z;

        if (Max_.x > Max.x)
            Max.x = Max_.x;
        if (Max_.y > Max.y)
            Max.y = Max_.y;
        if (Max_.z > Max.z)
            Max.z = Max_.z;

        Current.mAABB.SetMinMax(Min, Max);
	}
	return true;
}


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Walks the tree and call the user back for each node.
 *	\param		callback	[in] walking callback
 *	\param		user_data	[in] callback's user data
 *	\return		true if success
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
bool AABBNoLeafTree::Walk(GenericWalkingCallback callback, void* user_data) const
{
	if(!callback)	return false;

	struct Local
	{
		static void _Walk(const AABBNoLeafNode* current_node, GenericWalkingCallback callback, void* user_data)
		{
			if(!current_node || !(callback)(current_node, user_data))	return;

			if(!current_node->HasPosLeaf())	_Walk(current_node->GetPos(), callback, user_data);
			if(!current_node->HasNegLeaf())	_Walk(current_node->GetNeg(), callback, user_data);
		}
	};
	Local::_Walk(mNodes, callback, user_data);
	return true;
}

