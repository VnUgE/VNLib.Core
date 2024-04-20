cmake_minimum_required(VERSION 3.10)

project(vnlib_rpmalloc C)
set(CMAKE_PROJECT_NAME "vnlib_rpmalloc")

option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

#Setup the compiler options 
set(CMAKE_C_STANDARD 11)					#c11 is required for rpmalloc static assertions
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)		#enable position independent code (for shared libraries with exports)

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

#export header files to the main project
file(GLOB HEADERS *.h)

#Add indepednent source files to the project
set(VNLIB_RPMALLOC_SOURCES 
	"vnlib_rpmalloc.c" 
	"vendor/rpmalloc.c"
)

#add rpmalloc includes, there will only be one library
include_directories(vendor)

#create shared/static libs
add_library(${CMAKE_PROJECT_NAME} SHARED ${VNLIB_RPMALLOC_SOURCES} ${HEADERS})
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${VNLIB_RPMALLOC_SOURCES} ${HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME vn_rpmalloc)
endif()


#setup flags for windows compilation
if(MSVC)

	add_compile_options(
		/Qspectre 
		/sdl
		/TC
		/GS 

		#disable warnings for struct padding and spectre mitigation when WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>
		$<$<CONFIG:Debug>:/wd4574>

		#for debug configs
		$<$<CONFIG:Debug>:/options:strict>
		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/WX>		#warnings as errors (only for our project)
		$<$<CONFIG:Debug>:/Zi>		#enable debug info
		$<$<CONFIG:Debug>:/Zo>	
		$<$<CONFIG:Debug>:/FC>		#full path in diagnostics
		$<$<CONFIG:Debug>:/showIncludes>
	)

	add_compile_definitions(
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	add_compile_options(
		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		add_compile_options(
			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)
	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()

#enable required features
add_compile_definitions(
	RPMALLOC_FIRST_CLASS_HEAPS=1
	ENABLE_ADAPTIVE_THREAD_CACHE=1

	#add some debugging/tracing for debug mode
	$<$<CONFIG:Debug>:ENABLE_TRACE=1>
	$<$<CONFIG:Debug>:ENABLE_VALIDATE_ARGS=1>
)

if(ENABLE_GREEDY)

	add_compile_definitions(
		#if greedy is enabled, add greedy options
		ENABLE_UNLIMITED_CACHE=1
		ENABLE_UNLIMITED_GLOBAL_CACHE=1
		ENABLE_UNLIMITED_THREAD_CACHE=1

		#On by default but we otherwise disable global cache to really reduce commited size
		ENABLE_GLOBAL_CACHE=1
		ENABLE_UNLIMITED_GLOBAL_CACHE=1
	)

else()

	add_compile_definitions(	
		#disable greedy definitions
		ENABLE_UNLIMITED_CACHE=0
		ENABLE_UNLIMITED_GLOBAL_CACHE=0
		ENABLE_UNLIMITED_THREAD_CACHE=0
		ENABLE_GLOBAL_CACHE=0

		#also disable unlimited global cache to release spans to OS
		ENABLE_UNLIMITED_GLOBAL_CACHE=0
	)

endif()
