(function (global) {
  'use strict';

  function icpMsg(key) {
    var template = (global.IcpI18n && global.IcpI18n[key]) || key;
    var args = Array.prototype.slice.call(arguments, 1);
    for (var i = 0; i < args.length; i++) {
      template = template.replace('{' + i + '}', args[i]);
    }
    return template;
  }

  global.icpMsg = icpMsg;
})(window);
