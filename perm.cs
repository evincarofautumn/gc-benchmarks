using System;
using System.Diagnostics;
using System.Text;

/*

Memory system benchmark using Zaks's permutation generator.

Original public-domain Scheme implementation written by Lars Hansen,
Will Clinger, and Gene Luks. Translated to C# by Jon Purdy.

------------------------------------------------------------------------

This benchmark is in four parts.  Each tests a different aspect of
the memory system.

    perm            storage allocation
    10perm          storage allocation and garbage collection
    sumperms        traversal of a large, linked, self-sharing structure
    mergesort!      side effects and write barrier

The perm9 benchmark generates a list of all 362880 permutations of
the first 9 integers, allocating 1349288 pairs (typically 10,794,304
bytes), all of which goes into the generated list.  (That is, the
perm9 benchmark generates absolutely no garbage.)  This represents
a savings of about 63% over the storage that would be required by
an unshared list of permutations.  The generated permutations are
in order of a grey code that bears no obvious relationship to a
lexicographic order.

The 10perm9 benchmark repeats the perm9 benchmark 10 times, so it
allocates and reclaims 13492880 pairs (typically 107,943,040 bytes).
The live storage peaks at twice the storage that is allocated by the
perm9 benchmark.  At the end of each iteration, the oldest half of
the live storage becomes garbage.  Object lifetimes are distributed
uniformly between 10.3 and 20.6 megabytes.

The sumperms benchmark computes the sum of the permuted integers
over all permutations.

The mergesort! benchmark destructively sorts the generated permutations
into lexicographic order, allocating no storage whatsoever.

The benchmarks are run by calling the following procedures:

   (perm-benchmark n)
   (tenperm-benchmark n)
   (sumperms-benchmark n)
   (mergesort-benchmark n)

The argument n may be omitted, in which case it defaults to 9.

These benchmarks assume that

   (RUN-BENCHMARK <string> <thunk> <count>)
   (RUN-BENCHMARK <string> <count> <thunk> <predicate>)

reports the time required to call <thunk> the number of times
specified by <count>, and uses <predicate> to test whether the
result returned by <thunk> is correct.

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
            if (Tail == null)
                return 1;
            return 1 + Tail.Length;
        }
    }

    /*
      (define (list-tail x n)
              (if (zero? n)
                  x
                  (list-tail (cdr x) (- n 1))))
    */
    public static List Drop (List x, int n)
    {
        return n == 0 || x == null ? x : Drop (x.Tail, n - 1);
    }

    /*
      (define (revloop x n y)
              (if (zero? n)
                  y
                  (revloop (cdr x)
                           (- n 1)
                           (cons (car x) y))))
    */
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
        
        for (; x != NULL; x = x->rest)
            for (y = x->first; y != NULL; y = y->rest)
                sum = sum + y->first;
        
        (define (sumlists x)
          (do ((x x (cdr x))
               (sum 0 (do ((y (car x) (cdr y))
                           (sum sum (+ sum (car y))))
                          ((null? y) sum))))
              ((null? x) sum)))
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
        
        (define (merge!! a b less?)
          (cond ((null? a) b)
                ((null? b) a)
                ((less? (car b) (car a))
                 (if (null? (cdr b))
                     (set-cdr! b a)
                     (merge-helper!! b a (cdr b)))
                 b)
                (else                           ; (car a) <= (car b)
                 (if (null? (cdr a))
                     (set-cdr! a b)
                     (merge-helper!! a (cdr a) b))
                 a)))
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
        /*
          (define (loop r a b)
            (if (less? (car b) (car a))
                (begin (set-cdr! r b)
                       (if (null? (cdr b))
                           (set-cdr! b a)
                           (loop b a (cdr b)) ))
                ;; (car a) <= (car b)
                (begin (set-cdr! r a)
                       (if (null? (cdr a))
                           (set-cdr! a b)
                           (loop a (cdr a) b)) )) )
        */
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
        
        (define (sort!! seq less?)
          (sort-helper seq less? (length seq)))
        */
        return SortHelper (ref seq, less, seq.Length);
    }

    private static List SortHelper (ref List seq, Func<object, object, bool> less, int n)
    {
        /*
          (define (step n)
            (cond ((> n 2)
                   (let* ((j (quotient n 2))
                          (a (step j))
                          (k (- n j))
                          (b (step k)))
                     (merge!! a b less?)))
                  ((= n 2)
                   (let ((x (car seq))
                         (y (cadr seq))
                         (p seq))
                     (set! seq (cddr seq))
                     (if (less? y x)
                         (begin
                          (set-car! p y)
                          (set-car! (cdr p) x)))
                     (set-cdr! (cdr p) '())
                     p))
                  ((= n 1)
                   (let ((p seq))
                     (set! seq (cdr seq))
                     (set-cdr! p '())
                     p))
                  (else
                   '())))
        */
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
        /*
            (define (one..n n)
              (do ((n n (- n 1))
                   (p '() (cons n p)))
                  ((zero? n) p)))
        */
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

}

class Test {

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

        if (arguments [0] == "MpermNKL" && arguments.Length == 5) {
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
            } catch (InvalidOperationException invalidOperation) {
                Console.Error.WriteLine (invalidOperation);
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

#if false
        var list = new List
            ((object)1, new List
            ((object)2, new List
            ((object)3, new List
            ((object)4, null))));
/*
        var x = Permutations (list);
        Console.WriteLine (x);
*/
        List.Sort (list, (a, b) => (int)a < (int)b);
        Console.WriteLine (list);
#endif

    }

    // Fills queue positions [i, j).
    private static void FillQueue (List[] queue, int n, int i, int j)
    {
        /*
            (define (fill-queue i j)
              (if (< i j)
                  (begin (vector-set! queue i (permutations (one..n n)))
                         (fill-queue (+ i 1) j))))
        */
        while (i < j) {
            queue [i] = Permutations (List.OneTo (n));
            ++i;
        }
    }

    // Removes L elements from queue.
    private static void FlushQueue (List[] queue, int k, int l)
    {

        /*
            (define (flush-queue)
              (let loop ((i 0))
                (if (< i k)
                    (begin (vector-set! queue
                                        i
                                        (let ((j (+ i ell)))
                                          (if (< j k)
                                              (vector-ref queue j)
                                              '())))
                           (loop (+ i 1))))))
        */

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
        /*
          (let ((x x)
          (perms (list x)))
          ...)
        */
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

	mono perm.exe MpermNKL <M:int> <N:int> <K:int> <L:int>

		M ≥ 0, N > 0, K > 0, 0 ≤ L ≤ K

		The 10perm9 benchmark is the 10perm9:2:1 special case of the
		MpermNKL benchmark, which allocates a queue of size K and then
		performs M iterations of the following operation:  Fill the queue
		with individually computed copies of all permutations of a list of
		size N, and then remove the oldest L copies from the queue.  At the
		end of each iteration, the oldest L/K of the live storage becomes
		garbage, and object lifetimes are distributed uniformly between two
		volumes that depend upon N, K, and L.

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
    public void Permute (int n)
    {
        /*
          (define (P n)
                  (if (> n 1)
                      (do ((j (- n 1) (- j 1)))
                          ((zero? j)
                           (P (- n 1)))
                          (P (- n 1))
                          (F n))))
        */
        if (n <= 1)
            return;
        for (int j = 0; j < n - 1; ++j) {
            Permute (n - 1);
            Flip (n);
        }
        Permute (n - 1);
    }
    /*
      (define (F n)
          (set! x (revloop x n (list-tail x n)))
          (set! perms (cons x perms)))
    */
    private void Flip (int n)
    {
        x = List.Reverse (x, n, List.Drop (x, n));
        perms = new List (x, perms);
    }
}

/*

(define lexicographically-less?
  (lambda (x y)
    (define (lexicographically-less? x y)
      (cond ((null? x) (not (null? y)))
            ((null? y) #f)
            ((< (car x) (car y)) #t)
            ((= (car x) (car y))
             (lexicographically-less? (cdr x) (cdr y)))
            (else #f)))
    (lexicographically-less? x y)))

; This procedure isn't used by the benchmarks,
; but is provided as a public service.

(define (internally-imperative-mergesort list less?)
  
  (define (list-copy l)
    (define (loop l prev)
      (if (null? l)
          #t
          (let ((q (cons (car l) '())))
            (set-cdr! prev q)
            (loop (cdr l) q))))
    (if (null? l)
        l
        (let ((first (cons (car l) '())))
          (loop (cdr l) first)
          first)))
  
  (sort!! (list-copy list) less?))

(define *perms* '())

(define (perm-benchmark . rest)
  (let ((n (if (null? rest) 9 (car rest))))
    (set! *perms* '())
    (run-benchmark (string-append "Perm" (number->string n))
                   1
                   (lambda ()
                     (set! *perms* (permutations (one..n n)))
                     #t)
                   (lambda (x) #t))))

(define (tenperm-benchmark . rest)
  (let ((n (if (null? rest) 9 (car rest))))
    (set! *perms* '())
    (MpermNKL-benchmark 10 n 2 1)))

(define (sumperms-benchmark . rest)
  (let ((n (if (null? rest) 9 (car rest))))
    (if (or (null? *perms*)
            (not (= n (length (car *perms*)))))
        (set! *perms* (permutations (one..n n))))
    (run-benchmark (string-append "Sumperms" (number->string n))
                   1
                   (lambda ()
                     (sumlists *perms*))
                   (lambda (x) #t))))

(define (mergesort-benchmark . rest)
  (let ((n (if (null? rest) 9 (car rest))))
    (if (or (null? *perms*)
            (not (= n (length (car *perms*)))))
        (set! *perms* (permutations (one..n n))))
    (run-benchmark (string-append "Mergesort!" (number->string n))
                   1
                   (lambda ()
                     (sort!! *perms* lexicographically-less?)
                     #t)
                   (lambda (x) #t))))
*/
