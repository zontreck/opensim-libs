OPENSIM README
this is version ODE-OpenSim.0.13.4 for ubODE
WARNING, do not forget to rename the dlls, they are still created named as *ode* read below

The ODE code in this repository correspondes to ODE release 0.13.1 r1902 with selected adictions from more recent vrsions and modifications by opensim 

= BUILD INSTRUCTIONS =

== WINDOWS ==
- open a comand prompt and change dir to ..\build and run:
for windows 32bits target:
	premake4 --only-single --platform=x32 --no-threading-intf  vs2008	
for windows 64bits target:
	premake4 --only-single --platform=x64 --no-threading-intf vs2008	
this will create a solution ode.sln for visual studio 2008 in build/vs2008

- open the ode.sln solution in visual studio 2008
- select the ReleaseDLL configuration, and platform (win32 or x64) acording to the target
- do a (menu)Build/Rebuild Solution
the ode.dll should be present in lib/ReleaseDLL
copy it to opensim bin/lib32/ubode.dll or bin/lib64/ubode.dll acording to platform

warning: current solution makes no distintion on platform and so compiles both to same locations. The ode.dll present at lib/ 
will be for the last platform compiled.

optionally you can produce a Debug version, selecting DebugDLL. Result will be at lib/DebugDLL. copy ode.dll and ode.pdb 
to bin/lib32 or bin/lib64 opensim folder acording to platform.
C++ debug does have a large impact on performance. You should only use it for testing. 

== On Linux ==
if you dont see the file ./configure you need to do
./bootstrap
to create it. Check it so see its dependencies on several linux tools.
you may need to do chmod +x bootstrap before since git keeps losing it

(could not test following adapted from justin instructions bellow)

== On Linux 32-bit ==
./configure --disable-asserts --enable-shared --disable-threading-intf 
make
cp ode/src/.libs/libubode.so.5.1.0 $OPENSIM/bin/lib32/libubode.so	 (possible name is not ..so.5.1.0 )

== On Linux 64-bit ==
./configure --disable-asserts --enable-shared --disable-threading-intf 
make
cp ode/src/.libs/libubode.so.5.1.0 $OPENSIM/bin/lib64/libubode-x86_64.so (possible name is not ..so.5.1.0 )

== On Linux 64-bit to cross-compile to 32-bit ==
CFLAGS=-m32 CPPFLAGS=-m32 LDFLAGS=-m32 ./configure --build=i686-pc-linux-gnu --disable-asserts --enable-shared --disable-threading-intf
make
cp ode/src/.libs/libubode.so.5.1.0 $OPENSIM/bin/lib32/libubode.so

you can run strip to remove debug information and reduce file size

you may need to ajdust files and bin/OpenSim.Region.PhysicsModule.ubOde.dll.config

== On Mac OS X Intel 64-bit ==
./configure --disable-asserts --enable-shared --disable-threading-intf 
make
cp ode/src/.libs/libubode.dylib $OPENSIM/bin/lib64/libubode.dylib (64bits)

engine ubOde shows ode.dll configuration in console and OpenSim.log similar to:
[ubODE] ode library configuration: ODE_single_precision ODE_OPENSIM OS0.13.4
