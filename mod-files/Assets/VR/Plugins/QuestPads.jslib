// Reads the Quest controllers straight from the browser's WebXR session (the
// "xr-standard" gamepad layout) into 16 floats for QuestInput.cs:
// [0..7] left hand, [8..15] right hand, each: stickX, stickY (up positive),
// trigger, grip, A/X, B/Y, stick click, 0. Returns how many hands were read.
mergeInto(LibraryManager.library, {
  QuestPads_Read: function (ptr) {
    var start = ptr >> 2;
    for (var k = 0; k < 16; k++) HEAPF32[start + k] = 0;
    var session = Module.WebXR && Module.WebXR.xrSession;
    if (!session || !session.inputSources) return 0;
    var hands = 0;
    for (var i = 0; i < session.inputSources.length; i++) {
      var source = session.inputSources[i];
      var pad = source.gamepad;
      if (!pad) continue;
      var base = source.handedness === 'left' ? 0 : (source.handedness === 'right' ? 8 : -1);
      if (base < 0) continue;
      var axes = pad.axes, buttons = pad.buttons;
      var x = axes.length > 3 ? axes[2] : (axes[0] || 0);
      var y = axes.length > 3 ? axes[3] : (axes[1] || 0);
      HEAPF32[start + base + 0] = x;
      HEAPF32[start + base + 1] = -y;
      HEAPF32[start + base + 2] = buttons[0] ? buttons[0].value : 0;
      HEAPF32[start + base + 3] = buttons[1] ? buttons[1].value : 0;
      HEAPF32[start + base + 4] = buttons[4] && buttons[4].pressed ? 1 : 0;
      HEAPF32[start + base + 5] = buttons[5] && buttons[5].pressed ? 1 : 0;
      HEAPF32[start + base + 6] = buttons[3] && buttons[3].pressed ? 1 : 0;
      hands++;
    }
    return hands;
  }
});
