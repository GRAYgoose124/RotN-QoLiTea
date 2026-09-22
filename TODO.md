- From Patachu (on Results divergence plot)
    - (done) Per-vibe: hit count + first/last monster names (fullscreen caption on gold bands)
    - (done) Option to make jukebox jump to loadout instead of auto play (`RandomSongOpenLoadout`)
    - (done) Miss+overhit same-time → vert reds (not pink overhit); tinted monster sprites when readable

- One of patas plots shows what look like erroneous overhit marks (instead of lines); colored like overhits, not misses; all aligned same lateness; wonder if rapid detects are misfiring or a bad old path.; yes they are real misses but no hit data or something strange; should be vert reds prob. yes, in fact i think it's from a miss and overhit at the same time. (fixed: ProcessHitClassification)
