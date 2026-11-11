#!/usr/bin/perl
# Gives a freshly unpacked copy of the game the same file IDs (GUIDs) as the
# copy the mod was made on, so the mod's files and changes point at the right
# things. AssetRipper picks new random IDs every time it unpacks; this puts
# the original ones back, by file name, using original-ids.txt (names and IDs
# only, nothing of the game).
# The sound mixers also keep IDs inside them, matched by line from
# inner-ids.txt (file, line, original ID).
# Usage: perl match-ids.pl <ExportedProject> <original-ids.txt> [inner-ids.txt]
use strict;
use warnings;
use File::Find;

my ($project, $list, $inner) = @ARGV;
die "usage: match-ids.pl <project> <original-ids.txt>\n" unless $project && $list;

my %original;
open(my $ids, '<', $list) or die "can't read $list\n";
while (<$ids>) {
    chomp;
    my ($path, $guid) = split /\t/;
    $original{$path} = $guid if $path && $guid;
}
close $ids;

# Each unpacked file's new ID, swapped for its original.
my %swap;
find({ no_chdir => 1, wanted => sub {
    return unless /\.meta$/ && -f $_;
    (my $rel = $_) =~ s/^\Q$project\E\/?//;
    $rel =~ s/\.meta$//;
    my $old = $original{$rel} or return;
    open(my $in, '<', $_) or return;
    while (<$in>) {
        if (/^guid: ([0-9a-f]{32})/) { $swap{$1} = $old if $1 ne $old; last; }
    }
    close $in;
} }, "$project/Assets");

# Every text file that names an ID (scenes, prefabs, materials, the .meta
# files themselves...) gets the original ones.
my $changed = 0;
find({ no_chdir => 1, wanted => sub {
    return unless -f $_ && -s $_ < 200_000_000;
    open(my $in, '<:raw', $_) or return;
    read($in, my $head, 5);
    my $yaml = defined $head && ($head eq '%YAML' || /\.meta$/);
    unless ($yaml) { close $in; return; }
    local $/;
    my $text = $head . (<$in> // '');
    close $in;
    my $count = ($text =~ s/\b([0-9a-f]{32})\b/exists $swap{$1} ? $swap{$1} : $1/ge);
    return unless $count;
    open(my $out, '>:raw', $_) or return;
    print $out $text;
    close $out;
    $changed++;
} }, "$project/Assets", "$project/ProjectSettings");

# IDs inside a file, by line: the one on that line, and everywhere else in
# the same file.
my %byfile;
if ($inner && open(my $in, '<', $inner)) {
    while (<$in>) {
        chomp;
        my ($file, $line, $guid) = split /\t/;
        push @{ $byfile{$file} }, [$line, $guid] if $file;
    }
    close $in;
}
for my $file (keys %byfile) {
    my $path = "$project/$file";
    open(my $in, '<:raw', $path) or next;
    my @lines = <$in>;
    close $in;
    my %local;
    for my $pair (@{ $byfile{$file} }) {
        my ($line, $guid) = @$pair;
        next unless $line <= @lines && $lines[$line - 1] =~ /\b([0-9a-f]{32})\b/;
        $local{$1} = $guid;
    }
    my $text = join('', @lines);
    $text =~ s/\b([0-9a-f]{32})\b/exists $local{$1} ? $local{$1} : $1/ge;
    open(my $out, '>:raw', $path) or next;
    print $out $text;
    close $out;
}

print scalar(keys %swap), " IDs matched, $changed files updated\n";
