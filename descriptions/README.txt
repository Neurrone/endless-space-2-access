AUDIO DESCRIPTIONS FOR ENDLESS SPACE 2

Everything in here is written to be read aloud by a screen reader at
600 words per minute, which is the rate the timings assume. If you change
that rate, the cues will still be in the right places but may run long.

Three files per cutscene, all with the same base name.

The .srt is the one to use. It is a standard subtitle file, each entry
holding one description with the exact start and end time it should be
spoken between.

The .txt is for reading. Plain prose, no tables. It lists every gap, what
is said in it, how much of the available room that used, and then the
model's own timestamped notes on what happens in the footage.

The .json is the full structured data, with per-cue start, end, word count
and budget. Use this if you feed the descriptions into text to speech.


WHAT IS IN EACH FOLDER

intros: 12 files, 126 cues, 1554 words total.
  Cravers, Hissho, Horatio, Lumeris, Nakalim, Riftborn, Sophons, Umbral Choir, Unfallen, United Empire, Vaulters, Vodyani

outros: 28 files, 91 cues, 1277 words total.
  Cravers Outro (Lost Not Returned), Cravers Outro (Lost Returned), Hissho Outro (Lost Not Returned), Hissho Outro (Lost Returned), Horatio Outro (Lost Not Returned), Horatio Outro (Lost Returned), Lumeris Outro (Lost Not Returned), Lumeris Outro (Lost Returned), Mezari Outro (Lost Not Returned), Mezari Outro (Lost Returned), Nakalim Outro (Lost Not Returned), Nakalim Outro (Lost Returned), Riftborn Outro (Lost Not Returned), Riftborn Outro (Lost Returned), Sheredyn Outro (Lost Not Returned), Sheredyn Outro (Lost Returned), Sophons Outro (Lost Not Returned), Sophons Outro (Lost Returned), Umbral Choir Outro (Lost Not Returned), Umbral Choir Outro (Lost Returned), Unfallen Outro (Lost Not Returned), Unfallen Outro (Lost Returned), United Empire Outro (Lost Not Returned), United Empire Outro (Lost Returned), Vaulters Outro (Lost Not Returned), Vaulters Outro (Lost Returned), Vodyani Outro (Lost Not Returned), Vodyani Outro (Lost Returned)

colonisation: 26 files, 93 cues, 1547 words total.
  Arctic, Arid, Ash, Atoll, Barren, Boreal, Desert, Forest, Gas Burning, Gas Cold, Gas Frozen, Gas Hot, Gas Temperate, Gas Warm, Ice, Jungle, Lava, Mediterranean, Monsoon, Ocean, Savannah, Snow, Steppes, Terran, Toxic, Tundra

metaplot: 3 files, 19 cues, 197 words total.
  Metaplot (Lost Not Returned), Metaplot (Lost Returned), Metaplot Victory (Lost Returned)


A NOTE ON THE OUTROS

Every outro has two dialogue variants, Lost Returned and Lost Not
Returned, because the ending branches on game state. The footage is the
same in both but the dialogue timing differs, so the descriptions sit in
different places. Both are provided, named in brackets. Pick the one
matching the ending being played.

Mezari and Sheredyn are United Empire endings and have no intro of their
own, which is why they appear only under outros.


A NOTE ON THE METAPLOT ENDINGS

These three close the galaxy's own story rather than any single faction's,
and they are alternatives. A playthrough arrives at one of them, not all
three. Unlike the outros they are three separate films of three different
lengths, so each was described directly against its own footage and there
are no variants to choose between.

One cue in these was written by hand rather than by the model: the closing
image of Metaplot Victory, where the description came in at well under half
the available room. The reason is recorded at the top of that file.


A NOTE ON TIMING

Descriptions never overlap dialogue. Each one ends at least three tenths
of a second before the next line starts, and every cue was checked to fit
its slot at 600 words per minute with an eight percent safety margin.

Some gaps are deliberately left silent. Where nothing on screen has
changed, silence was chosen over filler.

The colonisation clips have no speech at all, so those descriptions are
fuller and written in complete sentences. They still leave the music
audible at the start, between cues, and at the end.
