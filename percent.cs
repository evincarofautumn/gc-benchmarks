using System;
class Test
{
    public static Int32 Main (String [] arguments)
    {
        if (arguments.Length != 2) {
            Console.Error.WriteLine ("Usage: percent <percentage> <objects>");
            return 1;
        }
        var percentage = (double)Int32.Parse (arguments [0]) / 100.0;
        var objects = Int32.Parse (arguments [1]);
        var array = new object [objects];
        for (var i = 0; i < objects; ++i)
            array [i] = new object ();
        var start = (int)(percentage * objects);
        using (var f = File.AppendText("output.txt"))
            f.WriteLine ("Reclaiming {0}/{1} objects.", start, objects);
        for (var i = start; i < objects; ++i)
            array [i] = null;
        GC.Collect ();
        return 0;
    }
}
