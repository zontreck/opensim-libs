///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
 *	OPCODE - Optimized Collision Detection
 *	Copyright (C) 2001 Pierre Terdiman
 *	Homepage: http://www.codercorner.com/Opcode.htm
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/**
 *	Contains compilation flags.
 *	\file		OPC_Settings.h
 *	\author		Pierre Terdiman
 *	\date		May, 12, 2001
 */
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Include Guard
#ifndef __OPC_SETTINGS_H__
#define __OPC_SETTINGS_H__

	//! Use CPU comparisons (comment that line to use standard FPU compares)
	//#define OPC_CPU_COMPARE

	//! Use epsilon value in tri-tri overlap test
	#define OPC_TRITRI_EPSILON_TEST

	//! Use tree-coherence or not [not implemented yet]
//	#define OPC_USE_TREE_COHERENCE

	//! Discard negative pointer in vanilla trees
	#define OPC_NO_NEG_VANILLA_TREE

	// NB: no compilation flag to enable/disable stats since they're actually needed in the box/box overlap test

#endif //__OPC_SETTINGS_H__
