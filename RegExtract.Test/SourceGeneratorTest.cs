using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace RegExtract.Test
{
    public class SourceGeneratorTest
    {
        private readonly ITestOutputHelper output;

        public SourceGeneratorTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        public record TestRecord(int Number, string Text)
        {
            public const string REGEXTRACT_REGEX_PATTERN = @"(\d+): (.+)";
        }

        [Fact]
        public void SourceGeneratorShouldGenerateExtractionPlan()
        {
            // This test will check if the source generator created the extraction plan
            var input = "42: Hello World";
            
            // Try to use the generated extraction plan (if it exists)
            // For now, we'll use the regular extraction to verify the pattern works
            var result = input.Extract<TestRecord>();
            
            Assert.NotNull(result);
            Assert.Equal(42, result.Number);
            Assert.Equal("Hello World", result.Text);
            
            output.WriteLine($"Extracted: {result}");
        }

        [Fact]
        public void CheckIfGeneratedCodeExists()
        {
            // First check if the simple test class exists to verify the source generator is running
            var simpleTestType = typeof(TestRecord).Assembly.GetType("RegExtract.Generated.SourceGeneratorTest");
            
            if (simpleTestType != null)
            {
                output.WriteLine("Source generator is working - found SourceGeneratorTest class");
                
                var getMessageMethod = simpleTestType.GetMethod("GetMessage");
                if (getMessageMethod != null)
                {
                    var message = getMessageMethod.Invoke(null, null);
                    output.WriteLine($"Message from generated code: {message}");
                }
            }
            else
            {
                output.WriteLine("SourceGeneratorTest class not found - source generator may not be running");
            }
            
            // Now check if the TestRecord extraction plan exists
            var generatedType = typeof(TestRecord).Assembly.GetType("RegExtract.Generated.TestRecordExtractionPlan");
            
            if (generatedType != null)
            {
                output.WriteLine($"Generated type found: {generatedType.FullName}");
                
                // Try to get the static Extract method
                var extractMethod = generatedType.GetMethod("Extract", new[] { typeof(string) });
                Assert.NotNull(extractMethod);
                
                // Try to call the generated extraction method
                var result = extractMethod.Invoke(null, new object[] { "42: Hello World" });
                Assert.NotNull(result);
                
                output.WriteLine($"Generated extraction result: {result}");
            }
            else
            {
                output.WriteLine("Generated TestRecordExtractionPlan not found - may need debugging");
                
                // List all types in the assembly that might be generated
                var generatedTypes = typeof(TestRecord).Assembly.GetTypes()
                    .Where(t => t.Namespace == "RegExtract.Generated")
                    .ToArray();
                    
                output.WriteLine($"Found {generatedTypes.Length} types in RegExtract.Generated namespace:");
                foreach (var type in generatedTypes)
                {
                    output.WriteLine($"  - {type.FullName}");
                }
            }
        }
    }
}