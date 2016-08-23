using System;
class Test
{
    public static Int32 Main (String [] arguments)
    {
        if (arguments.Length != 1) {
            Console.Error.WriteLine ("Usage: only-garbage <iterations>");
            return 1;
        }
        var iterations = Int32.Parse (arguments [0]);
        for (int i = 0; i < iterations; ++i)
            new object ();
        return 0;
    }
}
