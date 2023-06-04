//
// Mono.Math.Prime.Generator.PrimeGeneratorBase.cs - Abstract Prime Generator
//
// Authors:
//	Ben Maurer
//
// Copyright (c) 2003 Ben Maurer. All rights reserved
//

namespace Mono.Math.Prime.Generator;

//[CLSCompliant(false)]
internal abstract class PrimeGeneratorBase
{
    public virtual ConfidenceFactor Confidence => ConfidenceFactor.Medium;

    public virtual PrimalityTest PrimalityTest => PrimalityTests.SmallPrimeSppTest;

    public virtual int TrialDivisionBounds => 4000;

    /// <summary>
    ///     Performs primality tests on bi, assumes trial division has been done.
    /// </summary>
    /// <param name="bi">A BigInteger that has been subjected to and passed trial division</param>
    /// <returns>False if bi is composite, true if it may be prime.</returns>
    /// <remarks>The speed of this method is dependent on Confidence</remarks>
    protected bool PostTrialDivisionTests(BigInteger bi)
    {
        return PrimalityTest(bi, Confidence);
    }

    public abstract BigInteger GenerateNewPrime(int bits);
}