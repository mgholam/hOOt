using NUnit.Framework;

namespace Unit
{
    public class Tests
    {
        [Test]
        public static void words()
        {
            string s = @"134,909.09090
.......................................
abcdefg..--=-
B.A.T. 
a.k.a. 
1111111111 111.2342314 ---------------- brain-dead c:\dir1\dir2
http://www.google.com/path1/path2
hoot.property
camelCase field
PascalCase property/    @test;pppp=1
.aaaaa  ..bbbbb        com.ionic.framework  bob@gmail.com filename.docx filename.pdf
";

            var d = new RaptorDB.tokenizer().GenerateWordFreq(s);
        }



    } //Unit.Tests
}
