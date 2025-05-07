
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <getopt.h>
#include <assert.h>

#include "span.h"

#define VERSION "1.0.0"

#ifndef VERSION
    #error "VERSION not defined"
#endif

static const struct option long_options[] = {
    {"help", no_argument, NULL, 'h'},
    {"version", no_argument, NULL, 'v'},
    {"input", required_argument, NULL, 'i'},
    {"output", required_argument, NULL, 'o'},
    {NULL, 0, NULL, 0}
};

static void print_usage(const char *progname)
{
    fprintf(stderr, "envsub - Environment Variable Substitution Tool\n");
    fprintf(stderr, "Version: %s - Copyright Vaughn Nugent\n", VERSION);
    fprintf(stderr, "Licensed under GNU GPLv2 \n");
    fprintf(stderr, "Usage: %s [options]\n", progname);
    fprintf(stderr, "Options:\n");
    fprintf(stderr, "  -h, --help       Show this help message\n");
    fprintf(stderr, "  -v, --version    Show version information\n");
    fprintf(stderr, "  -i, --input      Input file (default: stdin)\n");
    fprintf(stderr, "  -o, --output     Output file (default: stdout)\n");
}

struct variable_line_pos 
{
    uint32_t start;
    uint32_t end;
};

static inline void printSpan(cspan_t span)
{
    fprintf(stderr, "Span: ");
    fwrite(
        spanGetOffsetC(span, 0),
        sizeof(char),
        spanGetSizeC(span),
        stderr
    );
    fprintf(stderr, "\n");
}

static void positionSet(struct variable_line_pos* position, uint32_t start, uint32_t end)
{
    position->start = start;
    position->end = end;
}

static uint32_t positionGetLen(const struct variable_line_pos* position)
{
    assert(position->end >= position->start && "Expected end position to be greater than start position");
    return position->end - position->start;
}

static int findStartAndEndPos(cspan_t line, struct variable_line_pos* position)
{

    int found = 0;

    //Default to the entire line span
    positionSet(position, 0, spanGetSizeC(line));

    for (uint32_t i = 0; i < spanGetSizeC(line); i++)
    {
        if (spanGetCharC(line, i) == '$')
        {
            found = 1;
			position->start = i - 1; // Set the start position just before the '$' character
            break;
        }
    }

    // See if any $ character was found
    if (!found)
    {
        return 0;
    }

    // Find the end of the variable name
    for (uint32_t i = position->start + 1; i < spanGetSizeC(line); i++)
    {
        char val = spanGetCharC(line, i);

        if (val == '}' && i + 1 < spanGetSizeC(line))
        {
            position->end = i + 1; // Set the end position after the bracket
            break;
        }

        //Find some standard end of sequence
        if (
            val == '\n' ||
            val == '\r' ||
            val == ' ' ||
            val == '\t' ||
            val == '\0'
        )
        {
            position->end = i; // Set the end position
            break;
        }
    }

    return position->start < position->end;
}

static cspan_t sliceVariableName(cspan_t line)
{
	// Should inlcude at least '$' and some variable name characters, 
    // $ alone is not valid
    if (spanGetSizeC(line) < 2)
    {
        return (cspan_t) { 0 };
    }

	// trim any leading characters and '$' character
    for (uint32_t startPos = 0; startPos < spanGetSizeC(line); startPos++)
    {
        if (spanGetCharC(line, startPos) == '$')
        {
            startPos++; // Exclude the leading '$' character

            if (spanGetCharC(line, startPos) == '{')
            {
                startPos++; // Exclude the optional '{' character
            }

            //Slice the line to exclude the leading '$' character
            line = spanSliceC(line, startPos, spanGetSizeC(line) - startPos);
            break;
        }
    }

	//Trim optional trailing } character
	for (uint32_t i = spanGetSizeC(line) - 1; i >= 0; i--)
	{
        if (spanGetCharC(line, i) == '}')
        {
			line = spanSliceC(line, 0, i);
			break;
        }
	}

    // The variable contained no useable characters after trimming
	if (spanGetSizeC(line) == 0)
	{
		return (cspan_t) { 0 };
	}

    // Check for default value syntax after trimming up
    for (uint32_t i = spanGetSizeC(line) - 1; i >= 0; i--)
    {
        if (spanGetCharC(line, i) == ':')
        {
            line = spanSliceC(line, 0, i);
            break;
        }
    }	

    // Return the start and end of the variable name
    return line;
}

static cspan_t sliceDefaultValue(cspan_t line)
{   

    // Find the start of the default value
    for (uint32_t i = 0; i < spanGetSizeC(line); i++)
    {
        if (spanGetCharC(line, i) == ':')
        {
            i++; // Exclude the ':' character

            if (spanGetCharC(line, i) == '-')
            {
                i++; // Exclude the optional '{' character
            }

            //Slice the line to exclude the leading '$' character
            line = spanSliceC(line, i, spanGetSizeC(line) - i);
            break;
        }
    }

 
    // Find the end of the default value
    for (uint32_t i = spanGetSizeC(line) - 1; i >= 0; i--)
    {
        char val = spanGetCharC(line, i);
        
        if (
            val == '}' ||
            val == '\n' ||
            val == '\r' ||
            val == ' ' ||
            val == '\t' ||
            val == '\0'
        )
        {
            line = spanSliceC(line, 0, i);
			break;
        }
    }	

    // Return the start and end of the default value
    return line;
}

static cspan_t getEnvFromVarName(cspan_t varName)
{
    // Try to read the environment variable by name
    char envVarName[256] = { 0 };
    if (spanGetSizeC(varName) >= sizeof(envVarName))
    {
		perror("Error: Environment variable name too long\n");
        return (cspan_t) { 0 };
    }

    // Copy to null terminated char buffer
    spanReadC(varName, envVarName, spanGetSizeC(varName));

	cspan_t envValueSpan = { 0 };

    // Check if the variable name is valid
    const char* envValue = secure_getenv(envVarName); 
    if (envValue)
    {
        // If the environment variable is found, return its value as a substitute        
        spanInitC(&envValueSpan, envValue, strlen(envValue));
    }

    return envValueSpan;
}

static cspan_t substituteLine(cspan_t line, struct variable_line_pos* varPos)
{
    if (!findStartAndEndPos(line, varPos))
    {
        // No variable found, return empty span
        return (cspan_t) { 0 };
    }

    // Assume the postions point to a valid range inside the line span
    assert(spanIsValidRangeC(line, varPos->start + 1, positionGetLen(varPos) - 1) && "Expected variable name to be within line span");

	line = spanSliceC(line, varPos->start, positionGetLen(varPos));

	printSpan(line); // Debugging line
  
    cspan_t varName = sliceVariableName(line);

    // If not found or empty, return the original line
    if (spanGetSizeC(varName) == 0)
    {
        // Set position to the original line
        positionSet(varPos, 0, spanGetSizeC(line));
        return (cspan_t) { 0 };
    }  

    // Check if the variable name is valid
    cspan_t envValue = getEnvFromVarName(varName);
    if (spanGetSizeC(envValue))
    {
		return envValue; // Return the environment variable value
    }    

    // Otherwise, return the default value or an empty span

    cspan_t defaultValue = sliceDefaultValue(line);
    return spanGetSizeC(defaultValue) > 0
        ? defaultValue
        : (cspan_t) { 0 }; // Return empty span if no default value is found
}

static cspan_t getStartSlice(cspan_t line, const struct variable_line_pos* varPos)
{    
    // Return the start of the line up to the variable name
    return spanSliceC(line, 0, varPos->start);
}

static cspan_t getEndSlice(cspan_t line, const struct variable_line_pos* varPos)
{
    // Return the end of the line after the variable name
    return spanSliceC(
        line, 
        varPos->end, 
        spanGetSizeC(line) - varPos->end
    );
}

static size_t writeSpan(FILE* output, cspan_t span)
{
    return fwrite(
        spanGetOffsetC(span, 0),
        sizeof(char),
        spanGetSizeC(span),
        output
    );
}

static int run_envsub(FILE* input, FILE* output)
{
    assert(input);
    assert(output);

    /*
     * The file needs to be read line by line, and each line needs to be checked for
     * the presence of standard shell variable syntax, with default values.
     * The syntax is ${VAR} or ${VAR:-default_value} or $VAR or $VAR:-default_value
     * The default value is used if the variable is not set or is empty.
     * The variable name can contain letters, numbers, and underscores.
     * The variable name cannot start with a number.
     */

    char line[1024];
    cspan_t lineSpan = { 0 };
    size_t written = 0;
    struct variable_line_pos varPos = { 0 };

    while (fgets(line, sizeof(line), input))
    {
        spanInitC(&lineSpan, line, strlen(line));

        cspan_t substitutedLine = substituteLine(lineSpan, &varPos);

        //Ensure the line positions point within the line span
        assert(spanIsValidRangeC(lineSpan, varPos.start, positionGetLen(&varPos)) && "Expected variable position to be within line span");

        //printSpan(substitutedLine);

        //Write data up to the variable name
        cspan_t startSlice = getStartSlice(lineSpan, &varPos);
        //written = writeSpan(output, startSlice);
        //assert(written == spanGetSizeC(startSlice) && "Expected all bytes to be written to output file");

        // Write variable value or default value
        //written = writeSpan(output, substitutedLine);
        //assert(written == spanGetSizeC(substitutedLine) && "Expected all bytes to be written to output file");

        // Write remaining data after the variable
        cspan_t endSlice = getEndSlice(lineSpan, &varPos);
        //written = writeSpan(output, endSlice);
        //assert(written == spanGetSizeC(endSlice) && "Expected all bytes to be written to output file");

        memset(line, 0, sizeof(line)); // Clear the line buffer before reading again
        memset(&varPos, 0, sizeof(varPos)); // Clear the variable position struct
    }

    return 0;
}

int main(int argc, char *argv[])
{
    int return_code = 0;

    if(argc < 1)
    {
        perror("Error: No arguments provided.\n");
        return 1;
    }

    int opt;

    FILE* input_file = NULL;
    FILE* output_file = NULL;

    // Parse command line options
    while ((opt = getopt_long(argc, argv, "hvio", long_options, NULL)) != -1) 
    {
        switch (opt) 
        {
            case 'i':
                input_file = fopen(optarg, "r");
                if (!input_file) 
                {
                    perror("Error opening input file");
                    return_code = 1;
                    goto Exit;
                }
                break;
            case 'o':
                output_file = fopen(optarg, "w");
                if (!output_file) 
                {
                    perror("Error opening output file");
                    return_code = 1;
                    goto Exit;
                }
                break;
            case 'h':
                print_usage(argv[0]);
              goto Exit;
            case 'v':
                printf("envsub version %s\n", VERSION);
                goto Exit;
            default:
                print_usage(argv[0]);
                return_code = 1;
                goto Exit;
        }
    }

    {
        // Input will be the input file if found, otherwise stdin
        FILE* in = (input_file) ? input_file : stdin;
        // Output will be the output file if found, otherwise stdout
        FILE* out = (output_file) ? output_file : stdout;

        //Run the envsub function
        return_code = run_envsub(in, out);
    }

Exit:

    if(input_file) fclose(input_file);
    if(output_file) fclose(output_file);

    return return_code;
}
