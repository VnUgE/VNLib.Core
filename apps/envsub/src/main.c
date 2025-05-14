
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


static size_t writeSpan(FILE* output, span_t span)
{
    return fwrite(
        spanGetOffset(span, 0),
        sizeof(char),
        spanGetSize(span),
        output
    );
}

static void writeSpanC(FILE* output, cspan_t span)
{
	fwrite(
		spanGetOffsetC(span, 0),
		sizeof(char),
		spanGetSizeC(span),
		output
	);
	fputc('\n', output); // Add a newline after writing the span
}

static inline int isEndChar(char value)
{
	return 
        value == '}'    || 
        value == '\n'   || 
        value == '\r'   || 
        value == ' '    || 
        value == '\t'   || 
        value == '\0'   || 
        value == '\\'   ||
        value == '\"';
}

static uint32_t findVarEnd(cspan_t line)
{
    // Find the first end delimiter after the '$' character
    for (uint32_t i = 0; i < spanGetSizeC(line); i++)
    {
        char val = spanGetCharC(line, i);

        //Find some standard end of sequence
        if (isEndChar(val))
        {
            return i;
        }
    }

    return spanGetSizeC(line);
}

static cspan_t sliceVariableName(cspan_t line)
{
    assert(spanGetCharC(line, 0) == '$' && "Assumed the first character to be a '$'");

	// Trim leading '$' character and optionally leading '{'
	line = spanGetCharC(line, 1) == '{'
		? spanSliceC(line, 2, spanGetSizeC(line) - 2)
		: spanSliceC(line, 1, spanGetSizeC(line) - 1);

	// Trim optional trailing '}' character
    if (spanGetCharC(line, spanGetSizeC(line) - 1) == '{')
    {
		line = spanSliceC(line, 0, spanGetSizeC(line) - 1);
    }

    // The variable contained no useable characters after trimming
	if (spanGetSizeC(line) == 0)
	{
		return (cspan_t) { 0 };
	}

    // Check for default value syntax after trimming up
    for (uint32_t i = spanGetSizeC(line) - 1; i > 0; i--)
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
    int found = 0;

    // Find the start of the default value
    for (uint32_t i = 0; i < spanGetSizeC(line); i++)
    {
        if (spanGetCharC(line, i) == ':')
        {
            i++; // Exclude the ':' character

            if (spanGetCharC(line, i) == '-')
            {
                i++; // Exclude the optional '-' character
            }

            //Slice the line to exclude the leading '$' character
            line = spanSliceC(line, i, spanGetSizeC(line) - i);
            found = 1;
            break;
        }
    }

    if (!found)
    {
		writeSpanC(stderr, line);
        return (cspan_t) { 0 };
    }
 
    // Find the end of the default value
    for (uint32_t i = spanGetSizeC(line) - 1; i > 0; i--)
    {
        char val = spanGetCharC(line, i);
        
        if (isEndChar(val))
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
    const char* envValue = getenv(envVarName); 
    if (envValue)
    {
        // If the environment variable is found, return its value as a substitute        
        spanInitC(&envValueSpan, envValue, strlen(envValue));
    }

    return envValueSpan;
}

static cspan_t substituteVariable(cspan_t line)
{  
    assert(spanGetCharC(line, 0) == '$' && "Assumed the first character to be a '$'");

    cspan_t varName = sliceVariableName(line);
    // If not found or empty, return the original line
    if (spanGetSizeC(varName) == 0)
    {
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

static int run_envsub(FILE *input, FILE *output)
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
	char outBuffer[2048];   //Output buffer may need to be larger incase the variable value is very large

    while (fgets(line, sizeof(line), input))
    {
        cspan_t lineSpan = { 0 };
		span_t outSpan = { 0 };
        uint32_t outputPosition = 0;
        
        spanInitC(&lineSpan, line, strlen(line));
		spanInit(&outSpan, outBuffer, sizeof(outBuffer));

		// Scan line for '$' characters
        for (uint32_t i = 0; i < spanGetSizeC(lineSpan); i++)
        {
            if (spanGetCharC(lineSpan, i) != '$')
            {
                goto Skip;
            }

			cspan_t varSlice = spanSliceC(lineSpan, i, spanGetSizeC(lineSpan) - i);

            /*
			* If a start of a variable is found, there must be an end. Find the end and always 
			* point to the end of the variable name.
            * 
			* If a variable is substituted, it will be written to the output buffer. Otherwise,
            * it will be skipped and stripped from the output, if there is no default value.
            */

			uint32_t varEnd = findVarEnd(varSlice);
        
            i += (varEnd - 1);

            // Trim the variable slice to the end of the variable name
            assert(spanIsValidRangeC(varSlice, 0, varEnd) && "Expected variable name to be within line span");
            varSlice = spanSliceC(varSlice, 0, varEnd);

            //Ensure there are enough characters to detect a variable name
            if (spanGetSizeC(varSlice) <= 2)
            {
                goto Skip;
            }

            cspan_t variable = substituteVariable(varSlice);
            if (spanGetSizeC(variable) == 0)
            {
				// If the variable is not found, skip and continue writing characters
				goto Skip;
            }         

            //Write the whole variable value to the output buffer            
            spanAppend(
                outSpan, 
                &outputPosition, 
                spanGetOffsetC(variable, 0),
                spanGetSizeC(variable)
            );

            // Force jump to the next itteration
            continue;

        Skip:;
            
            // If no variable is found, write the current character to the output
            const char* val = spanGetOffsetC(lineSpan, i);
            spanAppend(outSpan, &outputPosition, val, 1);
        }

        //Write the output line to the file descriptor
		if (outputPosition > 0)
		{
			outSpan = spanSlice(outSpan, 0, outputPosition);

			// Write the output line to the output file
			size_t written = writeSpan(output, outSpan);
			assert(written == outputPosition && "Expected all bytes to be written to output file");

			fflush(output); // Flush the output buffer
		}

        memset(line, 0, sizeof(line)); // Clear the line buffer before reading again
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
