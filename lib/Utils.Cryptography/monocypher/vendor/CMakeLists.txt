cmake_minimum_required(VERSION 3.0)

project(Monocypher C)

#export header files to the main project
file(GLOB HEADERS src/*.h)

#Add indepednent source files to the project
set(MONOCYPHER_SRCS 
	src/monocypher.c
)

#create shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${MONOCYPHER_SRCS} ${HEADERS})
#also create static library
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${MONOCYPHER_SRCS} ${HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME _monocypher)
endif()

#Setup the compiler options 
set(CMAKE_C_STANDARD 99)
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
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	#global gcc cl flags
	add_compile_options(
		-Wextra
		-fstack-protector
		-march=native

		$<$<CONFIG:Debug>:-g>
		$<$<CONFIG:Debug>:-Og>
		$<$<CONFIG:Debug>:-Wall>
		$<$<CONFIG:Debug>:-pedantic>
	)

endif()
