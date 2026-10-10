// Same test as Babylon's IS_TOUCH (src/render/Stage.ts): a coarse primary pointer.
// iPadOS Safari reports a desktop user agent, so Unity's isMobilePlatform misses it.
mergeInto(LibraryManager.library, {
  WZ_IsCoarsePointer: function () {
    return (typeof matchMedia !== 'undefined' && matchMedia('(pointer: coarse)').matches) ? 1 : 0;
  },
  // The page's real pointer lock (Esc releases it in the browser, not through Unity).
  WZ_PointerLocked: function () {
    return (typeof document !== 'undefined' && document.pointerLockElement) ? 1 : 0;
  },
});
