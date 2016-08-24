#!/usr/bin/env perl

use warnings;
use strict;

if (@ARGV != 1) {
  print STDERR "Usage: run-benchmark.pl <benchmark.exe>";
  exit (1);
}

my $benchmark = shift @ARGV;
my $iterations = 5;
my $orders_of_magnitude = 6;

my @a = ();
for my $i (0 .. $orders_of_magnitude) {
  push @a, (1 * 10 ** $i, 2 * 10 ** $i, 5 * 10 ** $i);
}

for my $input (@a) {
  print "$input";
  my $real_sum = 0;
  my $user_sum = 0;
  my $sys_sum = 0;
  for my $iteration (1 .. $iterations) {
    my $results = qx(time mono $benchmark $input 2>&1);
    if ($results =~ /([0-9\.]+)\s+real\s+([0-9\.]+)\s+user\s+([0-9\.]+)\s+sys/) {
      my $real = $1;
      my $user = $2;
      my $sys = $3;
      $real_sum += $real;
      $user_sum += $user;
      $sys_sum += $sys;
    }
  }
  my $real_mean = $real_sum / $iterations;
  my $user_mean = $user_sum / $iterations;
  my $sys_mean = $sys_sum / $iterations;
  print "\t$real_mean\n";
}
