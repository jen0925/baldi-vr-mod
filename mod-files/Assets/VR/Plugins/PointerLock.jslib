// Tells the page whether the game wants the mouse locked to it (its own hand
// cursor, as on a PC). The page locks it on the next click or key press, since
// browsers only allow that during one; when the game lets go (the launcher, VR)
// the mouse is released and the normal pointer comes back.
mergeInto(LibraryManager.library, {
  PointerLock_Want: function (want) {
    window.baldiWantsPointerLock = !!want;
    var canvas = Module.canvas;
    if (canvas) canvas.style.cursor = want ? 'none' : 'default';
    if (!want && document.pointerLockElement) document.exitPointerLock();
  }
});
