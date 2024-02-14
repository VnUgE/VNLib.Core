cmake_minimum_required(VERSION 3.0)

project(vnlib_rpmalloc C)

#export header files to the main project
file(GLOB HEADERS *.h)

#Add indepednent source files to the project
set(VNLIB_RPMALLOC_SOURCES 
	"vnlib_rpmalloc.c" 
	"vendor/rpmalloc.c"
)

#add rpmalloc includes, there will only be one library
include_directories(vendor)

#set options for greedy allocator defaults
option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)

#create my shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${VNLIB_RPMALLOC_SOURCES} ${HEADERS})
#also create static library
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${VNLIB_RPMALLOC_SOURCES} ${HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME vn_rpmalloc)
endif()

#Setup the compiler options 
set(CMAKE_C_STANDARD 90)
set(CMAKE_C_STANDARD_REQUIRED ON)

#enable position independent code (for shared libraries with exports)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

message(STATUS "Build type is '${CMAKE_BUILD_TYPE}'")

#if debug
add_compile_definitions($<$<CONFIG:Debug>:DEBUG>)

if(ENABLE_GREEDY)
	add_compile_definitions(VNLIB_RPMALLOC_GREEDY)
endif()

#setup flags for windows compilation
if(MSVC)
	
    #global windows cl flags
	add_compile_options(
		/Qspectre 
		/sdl
		/TC
		/GS 
		/machine:x64
		
		$<$<CONFIG:Debug>:/FC>
		$<$<CONFIG:Debug>:/showIncludes>		
	)

	#only target our project
	target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE

		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>
		$<$<CONFIG:Debug>:/wd4574>

		#for debug configs
		$<$<CONFIG:Debug>:/options:strict>
		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/WX>	#warnings as errors (only for our project)
		$<$<CONFIG:Debug>:/Zi>
		$<$<CONFIG:Debug>:/Zo>
	)

	#set build macros
	add_compile_definitions( 
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	add_compile_options(
		-Wextra
		-fstack-protector

		$<$<CONFIG:Debug>:-g>
		$<$<CONFIG:Debug>:-Og>
		$<$<CONFIG:Debug>:-Wall>
		$<$<CONFIG:Debug>:-Werror>
	)

	#only target our project
    target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE
		$<$<CONFIG:Debug>:-Wall>
		$<$<CONFIG:Debug>:-pedantic>
	)

endif()

#enable required features
add_definitions(-DRPMALLOC_FIRST_CLASS_HEAPS=1)
add_definitions(-DENABLE_ADAPTIVE_THREAD_CACHE=1)

#add some debugging/tracing for debug mode
add_compile_definitions($<$<CONFIG:Debug>:ENABLE_VALIDATE_ARGS=1>)
add_compile_definitions($<$<CONFIG:Debug>:ENABLE_ASSERTS=1>)

if(ENABLE_GREEDY)

#if greedy is enabled, add greedy options
add_definitions(-DENABLE_UNLIMITED_CACHE=1)
add_definitions(-DENABLE_UNLIMITED_GLOBAL_CACHE=1)
add_definitions(-DENABLE_UNLIMITED_THREAD_CACHE=1)

#On by default but we otherwise disable global cache to really reduce commited size
add_definitions(-DENABLE_GLOBAL_CACHE=1)
add_definitions(-DENABLE_UNLIMITED_GLOBAL_CACHE=1)

else()

#disable greedy definitions
add_definitions(-DENABLE_UNLIMITED_CACHE 0)
add_definitions(-DENABLE_UNLIMITED_GLOBAL_CACHE=0)
add_definitions(-DENABLE_UNLIMITED_THREAD_CACHE=0)
add_definitions(-DENABLE_GLOBAL_CACHE=0)

#also disable unlimited global cache to release spans to OS
add_definitions(-DENABLE_UNLIMITED_GLOBAL_CACHE=0)

endif()

