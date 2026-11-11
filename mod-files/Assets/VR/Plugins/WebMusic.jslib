// Plays the music the game's own MIDI synth makes in the browser (MidiSynth,
// web build): each block of samples is queued on the page's audio context
// straight after the one before, so it plays without gaps.
mergeInto(LibraryManager.library, {
  BaldiMusicRate: function () {
    var ctx = (typeof WEBAudio !== 'undefined') ? WEBAudio.audioContext : null;
    return ctx ? ctx.sampleRate : 48000;
  },
  BaldiMusicQueued: function (id) {
    var ctx = (typeof WEBAudio !== 'undefined') ? WEBAudio.audioContext : null;
    if (!ctx) return 1;
    var q = (window.__baldiMusic = window.__baldiMusic || {})[id];
    return q ? Math.max(0, q.next - ctx.currentTime) : 0;
  },
  BaldiMusicPush: function (id, ptr, frames, volume) {
    var ctx = (typeof WEBAudio !== 'undefined') ? WEBAudio.audioContext : null;
    if (!ctx || ctx.state !== 'running') return;
    var all = (window.__baldiMusic = window.__baldiMusic || {});
    var q = all[id];
    if (!q) {
      q = all[id] = { next: 0, gain: ctx.createGain() };
      q.gain.connect(ctx.destination);
    }
    q.gain.gain.value = volume;
    var buffer = ctx.createBuffer(2, frames, ctx.sampleRate);
    var left = buffer.getChannelData(0), right = buffer.getChannelData(1);
    var base = ptr >> 2;
    for (var i = 0; i < frames; i++) {
      left[i] = HEAPF32[base + i * 2];
      right[i] = HEAPF32[base + i * 2 + 1];
    }
    var source = ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(q.gain);
    var start = Math.max(q.next, ctx.currentTime + 0.04);
    source.start(start);
    q.next = start + frames / ctx.sampleRate;
    q.pushed = (q.pushed || 0) + 1;
  }
});
