/*

Memory system benchmark using Zaks's permutation generator.

Original public-domain Scheme implementation written by Lars Hansen,
Will Clinger, and Gene Luks. Translated to C# by Jon Purdy.

------------------------------------------------------------------------

This benchmark is in four parts.  Each tests a different aspect of
the memory system.

    Perm            storage allocation
    10Perm          storage allocation and garbage collection
    SumPerms        traversal of a large, linked, self-sharing structure
    MergeSort       side effects and write barrier

*/

using System;
using System.Diagnostics;
using System.Text;

class List {

    public object Head { get; set; }
    public List Tail { get; set; }

    public List (object head, List tail) {
        this.Head = head;
        this.Tail = tail;
    }

    public int Length
    {
        get
        {
            int result = 0;
            for (var a = this; a != null; a = a.Tail)
                ++result;
            return result;
        }
    }

    public static List Drop (List x, int n)
    {
        return n == 0 || x == null ? x : Drop (x.Tail, n - 1);
    }

    public static List Reverse (List x, int n, List acc)
    {
        return n == 0 || x == null ? acc : List.Reverse (x.Tail, n - 1, new List (x.Head, acc));
    }

    public override string ToString ()
    {
        var result = new StringBuilder ();
        result.Append ("[");

        for (var p = this; p != null; p = p.Tail) {
            result.Append (p.Head);
            if (p.Tail != null)
                result.Append (", ");
        }

        result.Append ("]");
        return result.ToString ();
    }

    public int Sums ()
    {
        /*
        Given a list of lists of numbers, returns the sum of the sums
        of those lists.
        */

        var sum = 0;
        for (var x = this; x != null; x = x.Tail)
            for (var y = (List)x.Head; y != null; y = y.Tail)
                sum += (int)((List)y).Head;

        return sum;

    }

    public static List Merge (List a, List b, Func<object, object, bool> less)
    {
        /*
        ; Destructive merge of two sorted lists.
        ; From Hansen's MS thesis.
        */
        if (a == null)
            return b;

        if (b == null)
            return a;

        if (less (b.Head, a.Head)) {
            if (b.Tail == null) {
                b.Tail = a;
                MergeHelper (b, a, b.Tail, less);
            }
            return b;
        }

        if (a.Tail == null) {
            a.Tail = b;
            MergeHelper (a, a.Tail, b, less);
        }

        return a;

    }

    private static void MergeHelper (List r, List a, List b, Func<object, object, bool> less)
    {
        do {

            if (less (b.Head, a.Head)) {

                r.Tail = b;

                if (b.Tail == null) {
                    b.Tail = a;
                    r = b;
                    b = b.Tail;
                    continue;
                }

            } else {

                r.Tail = a;

                if (a.Tail == null) {
                    a.Tail = b;
                    r = a;
                    a = a.Tail;
                    continue;
                }

            }
        } while (false);
    }

    public static List Sort (List seq, Func<object, object, bool> less)
    {
        /*
        ;; Stable sort procedure which copies the input list and then sorts
        ;; the new list imperatively.  On the systems we have benchmarked,
        ;; this generic list sort has been at least as fast and usually much
        ;; faster than the library's sort routine.
        ;; Due to Richard O'Keefe; algorithm attributed to D.H.D. Warren.
        */
        return SortHelper (ref seq, less, seq.Length);
    }

    private static List SortHelper (ref List seq, Func<object, object, bool> less, int n)
    {
        if (n > 2) {

            var j = n / 2;
            var a = SortHelper (ref seq, less, j);
            var k = n - j;
            var b = SortHelper (ref seq, less, k);
            return Merge (a, b, less);

        } else if (n == 2) {

            var x = seq.Head;
            var y = seq.Tail.Head;
            var p = seq;

            seq = seq.Tail.Tail;

            if (less (y, x)) {
                p.Head = y;
                p.Tail.Head = x;
            }

            p.Tail.Tail = null;
            return p;

        } else if (n == 1) {

            var p = seq;
            seq = seq.Tail;
            p.Tail = null;
            return p;

        } else {
            return null;
        }

    }

    public static List OneTo (int n)
    {
        List result = null;
        for (int i = n + 1; i > 1; --i)
            result = new List ((object)(i - 1), result);
        return result;
    }

    public bool Equals (List that)
    {
        if (that == null)
            return false;

        var a = this;
        var b = that;

        while (a != null && b != null) {
            if (!a.Head.Equals (b.Head))
                return false;
            a = a.Tail;
            b = b.Tail;
        }

        return (a == null) && (b == null);
    }

    public static bool LexicographicallyLess (List x, List y, Func<object, object, bool> less)
    {
        while (true) {
            if (x == null)
                return y != null;
            if (y == null)
                return false;
            if (less (x.Head, y.Head))
                return true;
            if (less (x.Head, y.Head) || less (y.Head, x.Head))
                return false;
            x = x.Tail;
            y = y.Tail;
        }

    }

    public static bool NumericallyLess (Object a, Object b)
    {
        return (int)a < (int)b;
    }

}

class Test {

    static List perms = null;

    public static int Main (string [] arguments)
    {

#if DEBUG
        TextWriterTraceListener myWriter = new ConsoleTraceListener ();
        Debug.Listeners.Add (myWriter);
#endif

        Debug.WriteLine (List.OneTo (5));

        if (arguments.Length < 1) {
            PrintUsage ();
            return 1;
        }

        if (String.Equals (
                arguments [0],
                "Perm",
                StringComparison.OrdinalIgnoreCase)
            && arguments.Length >= 1 && arguments.Length <= 2) {

            int n = 9;
            if (arguments.Length == 2
                && !(Int32.TryParse (arguments [1], out n))) {
                Console.Error.WriteLine (
                    "Invalid arguments to Perm benchmark.",
                    arguments [0]);
                PrintUsage ();
                return 1;
            }

            try {
                RunBenchmark (
                    String.Format ("Perm{0}", n),
                    () => {
                        perms = Permutations (List.OneTo (n));
                        return perms;
                    },
                    1,
                    (x) => true);
            } catch (InvalidOperationException invalid_operation) {
                Console.Error.WriteLine (invalid_operation.Message);
                return 1;
            }

            return 0;

        }

        if (String.Equals (
                arguments [0],
                "10Perm",
                StringComparison.OrdinalIgnoreCase)
            && arguments.Length >= 1 && arguments.Length <= 2) {

            int n = 9;
            if (arguments.Length == 2
                && !(Int32.TryParse (arguments [1], out n))) {
                Console.Error.WriteLine (
                    "Invalid arguments to 10Perm benchmark.",
                    arguments [0]);
                PrintUsage ();
                return 1;
            }

            try {
                MPermNKLBenchmark (10, n, 2, 1);
            } catch (InvalidOperationException invalid_operation) {
                Console.Error.WriteLine (invalid_operation.Message);
                return 1;
            }

            return 0;

        }

        if (String.Equals (
                arguments [0],
                "MpermNKL",
                StringComparison.OrdinalIgnoreCase)
            && arguments.Length == 5) {

            int m, n, k, l;
            if (!(Int32.TryParse (arguments [1], out m)
                  && Int32.TryParse (arguments [2], out n)
                  && Int32.TryParse (arguments [3], out k)
                  && Int32.TryParse (arguments [4], out l)
                  && m >= 0
                  && n > 0
                  && k > 0
                  && (0 <= l && l <= k))) {
                Console.Error.WriteLine (
                    "Invalid arguments to {0} benchmark.",
                    arguments [0]);
                PrintUsage ();
                return 1;
            }

            try {
                MPermNKLBenchmark (m, n, k, l);
            } catch (InvalidOperationException invalid_operation) {
                Console.Error.WriteLine (invalid_operation.Message);
                return 1;
            }

            return 0;
        }

        if (String.Equals (
                arguments [0],
                "MergeSort",
                StringComparison.OrdinalIgnoreCase)
            && arguments.Length >= 1 && arguments.Length <= 2) {

            int n = 9;
            if (arguments.Length == 2
                && !(Int32.TryParse (arguments [1], out n))) {
                Console.Error.WriteLine (
                    "Invalid arguments to 10Perm benchmark.",
                    arguments [0]);
                PrintUsage ();
                return 1;
            }

            if (perms == null || n != ((List)perms.Head).Length)
                perms = Permutations (List.OneTo (n));

            try {
                RunBenchmark (
                    String.Format ("MergeSort!{0}", n),
                    () => {
                        List.Sort (
                            perms,
                            (a, b) => List.LexicographicallyLess (
                                (List)a, (List)b, List.NumericallyLess));
                        return perms;
                    },
                    1,
                    (x) => true);
            } catch (InvalidOperationException invalid_operation) {
                Console.Error.WriteLine (invalid_operation.Message);
                return 1;
            }

            return 0;

        }
        
        if (String.Equals (
                arguments [0],
                "SumPerms",
                StringComparison.OrdinalIgnoreCase)
            && arguments.Length >= 1 && arguments.Length <= 2) {

            int n = 9;
            if (arguments.Length == 2
                && !(Int32.TryParse (arguments [1], out n))) {
                Console.Error.WriteLine (
                    "Invalid arguments to SumPerms benchmark.",
                    arguments [0]);
                PrintUsage ();
                return 1;
            }

            if (perms == null || n != ((List)perms.Head).Length)
                perms = Permutations (List.OneTo (n));

            try {
                RunBenchmark (
                    String.Format ("MergeSort!{0}", n),
                    () => {
                        return perms.Sums ();
                    },
                    1,
                    (sum) => {
                        var n_factorial = 1;
                        for (int i = 1; i <= n; ++i)
                            n_factorial *= i;
                        return sum == n_factorial * n * (n + 1) / 2;
                    });
            } catch (InvalidOperationException invalid_operation) {
                Console.Error.WriteLine (invalid_operation.Message);
                return 1;
            }

            return 0;

        }

        Console.Error.WriteLine (
            "Unknown benchmark {0} or wrong number of arguments {1}.",
            arguments [0],
            arguments.Length);
        PrintUsage ();
        return 1;

    }

    // Fills queue positions [i, j).
    private static void FillQueue (List[] queue, int n, int i, int j)
    {
        while (i < j) {
            queue [i] = Permutations (List.OneTo (n));
            ++i;
        }
    }

    // Removes L elements from queue.
    private static void FlushQueue (List[] queue, int k, int l)
    {
        int i = 0;
        while (i < k) {
            int j = i + l;
            queue [i] = j < k ? queue [j] : null;
            ++i;
        }
    }

    private static void RunBenchmark<T>
        (string id, Func<T> benchmark, int count, Func<T, bool> correct)
    {
        var stopwatch = new Stopwatch ();
        Debug.WriteLine ("Running benchmark {0}.", id);
        stopwatch.Start ();
        for (int i = 0; i < count; ++i) {
            Debug.WriteLine ("Benchmark {0} iteration {1}.", id, i);
            var result = benchmark ();
            if (!correct (result))
                throw new InvalidOperationException
                    (String.Format ("Benchmark {0} failed (returned {1}).", id, result));
        }
        stopwatch.Stop ();
        Console.WriteLine (
            "Benchmark {0} succeeded ({1} iters, {2}ms / iter).",
            id,
            count,
            (double)stopwatch.ElapsedMilliseconds / count);
    }

    private static void MPermNKLBenchmark (int m, int n, int k, int l)
    {
        var id = String.Format ("{0}perm{1}:{2}:{3}", m, n, k, l);
        var queue = new List [k];
        FillQueue (queue, n, 0, k - l);
        RunBenchmark (
            id,
            () => {
                Debug.WriteLine ("Filling queue...");
                FillQueue (queue, n, k - l, k);
                Debug.WriteLine ("Flushing queue...");
                FlushQueue (queue, k, l);
                Debug.WriteLine ("Done.");
                return queue;
            },
            m,
            (q) => {
                var q0 = q [0];
                var qi = q [Math.Max (0, k - l - 1)];
                return q0 == null && qi == null || q0 != null && qi != null && q0.Head == qi.Head;
            });
    }

    public static List Permutations (List x)
    {
        var permuter = new Permuter (x);
        permuter.Permute (x.Length);
        return permuter.Permutations;
    }

    private static void PrintUsage ()
    {
        Console.Error.WriteLine (
@"Usage:

	mono perm.exe <benchmark> <options>

Benchmarks:

	mono perm.exe Perm <N:int>?
		default N = 9

		The perm9 benchmark generates a list of all 362880 permutations of
		the first 9 integers, allocating 1349288 pairs (typically 10,794,304
		bytes), all of which goes into the generated list.  (That is, the
		perm9 benchmark generates absolutely no garbage.)  This represents
		a savings of about 63% over the storage that would be required by
		an unshared list of permutations.  The generated permutations are
		in order of a grey code that bears no obvious relationship to a
		lexicographic order.

	mono perm.exe 10Perm <N:int>?
		default N = 9

		The 10perm9 benchmark repeats the perm9 benchmark 10 times, so it
		allocates and reclaims 13492880 pairs (typically 107,943,040 bytes).
		The live storage peaks at twice the storage that is allocated by the
		perm9 benchmark.  At the end of each iteration, the oldest half of
		the live storage becomes garbage.  Object lifetimes are distributed
		uniformly between 10.3 and 20.6 megabytes. The 10perm9 benchmark is
		the 10perm9:2:1 special case of the MpermNKL benchmark.

	mono perm.exe MpermNKL <M:int> <N:int> <K:int> <L:int>
		where M ≥ 0, N > 0, K > 0, 0 ≤ L ≤ K

		Allocates a queue of size K and then performs M iterations of the
		following operation: Fill the queue with individually computed
		copies of all permutations of a list of size N, and then remove the
		oldest L copies from the queue.  At the end of each iteration, the
		oldest L/K of the live storage becomes garbage, and object lifetimes
		are distributed uniformly between two volumes that depend upon N, K,
		and L.

	mono perm.exe SumPerms <N:int>?
		default N = 9

		The sumperms benchmark computes the sum of the permuted integers
		over all permutations.

	mono perm.exe MergeSort <N:int>?
		default N = 9

		The mergesort! benchmark destructively sorts the generated permutations
		into lexicographic order, allocating no storage whatsoever.

");
    }

}

class Permuter
{

    private List x, perms;

    public List Permutations
    {
        get
        {
            return perms;
        }
    }

    public Permuter (List x)
    {
        this.x = x;
        this.perms = new List (x, null);
    }

/*

Date: Thu, 17 Mar 94 19:43:32 -0800
From: luks@sisters.cs.uoregon.edu
To: will
Subject: Pancake flips

Procedure P_n generates a grey code of all perms of n elements
on top of stack ending with reversal of starting sequence

F_n is flip of top n elements.

procedure P_n

  if n>1 then
    begin
       repeat   P_{n-1},F_n   n-1 times;
       P_{n-1}
    end

*/

    public void Permute (int n)
    {
        if (n <= 1)
            return;

        for (int j = 0; j < n - 1; ++j) {
            Permute (n - 1);
            Flip (n);
        }

        Permute (n - 1);
    }

    private void Flip (int n)
    {
        x = List.Reverse (x, n, List.Drop (x, n));
        perms = new List (x, perms);
    }

}
