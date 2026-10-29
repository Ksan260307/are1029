mergeInto(LibraryManager.library, {
  // Short vibration on devices and browsers that support it (no-op elsewhere).
  NR_Vibrate: function (milliseconds) {
    try {
      if (typeof navigator !== "undefined" && navigator.vibrate) navigator.vibrate(milliseconds);
    } catch (e) { }
  },
});
