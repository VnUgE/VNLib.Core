cmake_minimum_required(VERSION 3.18)

project(
	Argon2 
	LANGUAGES C
	DESCRIPTION "CMake wrapper for the Argon2 reference library"
	HOMEPAGE_URL "https://www.vaughnnugent.com/resources/modules/vnlib.core"
)

#export header files to the main project
file(GLOB HEADERS src/*.h)

#Add indepednent source files to the project
set(ARGON_SRCS 
	src/argon2.c
	src/core.c
	src/encoding.c

	src/opt.c
	src/thread.c
	src/blake2/blake2b.c
)

#add include directory
include_directories(./include ./src/blake2)

#create shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${ARGON_SRCS} ${HEADERS})
#also create static library
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${ARGON_SRCS} ${HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME argon2)
endif()

#Setup the compiler options 
set(CMAKE_C_STANDARD 90)
set(CMAKE_C_STANDARD_REQUIRED ON)

#enable position independent code (for shared libraries with exports)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

message(STATUS "Build type is '${CMAKE_BUILD_TYPE}'")

#if debug
add_compile_definitions($<$<CONFIG:Debug>:DEBUG>)

#setup flags for windows compilation
if(MSVC)
	
    #global windows cl flags
	add_compile_options(
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
		$<$<CONFIG:Debug>:/Zi>
		$<$<CONFIG:Debug>:/Zo>
	)

	#set build macros
	add_compile_definitions( 
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
		_CRT_SECURE_NO_WARNINGS		#NOTE! from Argon2 offical project files, supresses warning about sprintf being an insecure function call
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	add_compile_options(
		-Wextra
		-fstack-protector

		$<$<CONFIG:Debug>:-g>
		$<$<CONFIG:Debug>:-Og>
		$<$<CONFIG:Debug>:-Wall>
	)

	#only target our project
    target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE
		$<$<CONFIG:Debug>:-Wall>
		$<$<CONFIG:Debug>:-pedantic>
	)

endif()

