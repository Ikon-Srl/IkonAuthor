
$(document).ready(function() {
  //trasparenza png per IE6
  $(document).pngFix();
  //
  ajaxLoadPostProcessor(document, false);
  //
  $('.autoSubmitDDL,.autoSubmitCB,.autoSubmitTB').live('change', function() { if ($.browser.msie) { $('iframe').remove(); } $(this).closest('form').submit(); });  // risolve i problemi con IE <= 7 e presenza di iframe con src su domini esterni
  //
  $.ajaxSetup({ timeout: parseInt($('#ajaxTimeOut').val() || '120000') });  // per eseguire l'override dei timeout ajax inserire nella pagina il codice <input type="hidden" id="ajaxTimeOut" name="ajaxTimeOut" value="3600000" />
  //$.ajaxSetup({ timeout: 3600000 });
  //
  // gestione dei tabs
  //var tmp01 = $('.jQueryUI_TabsAuto');
  //alert($('ul:first li.selected', tmp01).index());
  try {
    var tabsBlock = $('.jQueryUI_TabsAuto');
    var my_tabs = tabsBlock.tabs({
      collapsible: true,
      //event: 'mouseover',
      selected: $('ul:first li.selected', tabsBlock).index(),
      load: function(event, ui) {
        // per processare le popup caricate nei moduli ajax
        try { $('a.autoPopup:has(img)', ui.panel).fancybox({}); } catch (e) { }
        try { $('a.autoPopupGallery:has(img)', ui.panel).attr('rel', 'gallery').fancybox({}); } catch (e) { }
        // per processare i tips
        //try { $('.tipTip[title]', ui.panel).tipTip({ maxWidth: 'auto', defaultPosition: 'top', edgeOffset: 0 }); } catch (e) { }
        //
        try {
          $('.qtip[title]', ui.panel).qtip({
            style: { name: 'light', width: { max: 300 }, tip: true },
            position: { target: 'mouse', corner: { target: 'topMiddle', tooltip: 'bottomLeft'} }
          });
        } catch (e) { }
        //
      },
      ajaxOptions: {
        error: function(xhr, status, index, anchor) {
          $(anchor.hash).html("AJAX ERROR.");
        }
      }
    });
    //my_tabs.find(".ui-tabs-nav").sortable({ axis: 'x' });
  } catch (e) { }
  //
  // gestione degli accordion automatici
  standardAccordionInitializer = function() {
    $('.jQueryUI_AccordionAutoStartClosed').accordion({
      collapsible: true,
      active: false,
      autoHeight: false,
      header: '.accordionHeader'
    });
    $('.jQueryUI_AccordionAutoStartClosed').accordion({
      collapsible: true,
      autoHeight: false,
      header: '.accordionHeader'
    });
  };
  try { standardAccordionInitializer(); } catch (e) { }
  //
  $.hrefProcessorHandler = function(link) {
    var url = link.attr('href');
    // aggiunge dinamicamente la querystring/hash UrlBack
    if (link.hasClass('append_UrlBack')) {
      try {
        var UrlBack = window.locationFake || window.location.toString();
        url = $.param.querystring(url, { UrlBack: EncodeBase64(UrlBack) }, 0);
        //url = $.param.fragment(url, { UrlBack: EncodeBase64(UrlBack) }, 0);
      }
      catch (ex) { }
    }
    if (link.hasClass('append_ReturnUrl')) {
      try {
        var ReturnUrl = window.location.toString();
        url = $.param.querystring(url, { ReturnUrl: ReturnUrl }, 0);
      }
      catch (ex) { }
    }
    // per la propagazione dell'hash da una url alle successive
    if (link.hasClass('append_hash')) {
      try {
        var frag = $.param.fragment();  // frag della url corrente
        if (frag != null && frag.length > 0)
          url = $.param.fragment(url, frag, 0);
      }
      catch (ex) { }
    }
    var target = link.attr('target') || '';
    if (target.length > 0) {
      window.open(url, target);
    }
    else {
      window.location = url;
    }
  };
  //
  // clickableBlock management
  //
  /*$('.clickableBlock:has(a.clickableBlockLink)').live({
    click: function(e) {
      try {
        e.preventDefault();
        e.stopPropagation();
      } catch (ex) { }
      $.hrefProcessorHandler($(this).find('a.clickableBlockLink'));
      return false;
    },
    mouseenter: function() { $(this).addClass('hover'); },
    mouseleave: function() { $(this).removeClass('hover'); }
  });*/
  if ($.browser.msie && parseInt($.browser.version, 10) < 9) {
    $(document).on({
      click: function(e) {
        try {
          e.preventDefault();
          e.stopPropagation();
        } catch (ex) { }
        return $.hrefProcessorHandler($(this).find('a.clickableBlockLink'), e);
      }
    }, '.clickableBlock:has(a.clickableBlockLink)');
  } else {
    $(document).on({
      click: function(e) {
        try {
          e.preventDefault();
          e.stopPropagation();
        } catch (ex) { }
        return $.hrefProcessorHandler($(this).find('a.clickableBlockLink'), e);
      },
      mouseenter: function(e) { $(this).addClass('hover'); },
      mouseleave: function(e) { $(this).removeClass('hover'); }
    }, '.clickableBlock:has(a.clickableBlockLink)');
  }
  //
  $('a.append_UrlBack:not(.clickableBlockLink)').live('click', function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    $.hrefProcessorHandler($(this));
    return false;
  });
  //
  // per la gestione della url di ritorno nei moduli di autentifica
  $('a.append_ReturnUrl').live('click', function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    $.hrefProcessorHandler($(this));
    return false;
  });
  //
  // basic ajax loading support
  //
  $('a.ajaxLoad').live('click', function(e) {
    var link = $(this);
    var containerSelector = '.ajaxLoadContainer';
    var container = $(this).closest(containerSelector);
    if (link.length == 0 || container.length == 0)
      return true;
    e.preventDefault();
    e.stopPropagation();
    var url = link.attr('href');
    var meta = $.extend(link.metadata(), container.metadata());
    container.load(url + ' ' + containerSelector, function() {
      window.locationFake = url;
      if (typeof (meta.ajaxLoadCallback) == 'function') {
        meta.ajaxLoadCallback.call(link, container);
      }
    });
    return false;
  });
  //
  // json forms management functions
  //
  $.formSubmitWithJsonReturnHandler = function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    //
    var data = {};
    if (($('input[name="successUrl"]', $(this).closest('form')).val() || '').length == 0)
      data.successUrl = $(this).closest('a').attr('href') || document.referrer;
    $(this).closest('form').ajaxSubmit({
      //beforeSubmit: function() { alert('beforeSubmit'); },
      type: 'POST',
      data: data,
      dataType: 'json',
      //complete: function() { window.locationFake = this.url; },
      success: function(result) {
        var tmpFn = function() {
          if (result.hasError == false && typeof (result.successUrl) == 'string')
            window.location = result.successUrl;
          else if (result.hasError == true && typeof (result.errorUrl) == 'string')
            window.location = result.errorUrl;
        };
        if (typeof (result.message) == 'string') {
          messageBox(result.message, result.hasError ? 'Errore' : 'Messaggio', tmpFn);
        }
        else {
          tmpFn();
        }
        //
      }
    });
  };
  //
  $('.formSubmitWithJsonReturn').unbind('click').click(function(e) { $.formSubmitWithJsonReturnHandler.call(this, e); return false; });
  //
  $('.formSubmitWithJsonReturnLive').live('click', function(e) { $.formSubmitWithJsonReturnHandler.call(this, e); return false; });
  //
  // json links management functions
  //
  $('.linkWithJsonReturn').live('click', function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    //
    var actionUrl = $(this).closest('a').attr('href');
    var confirm = $(this).closest('a').attr('confirm');
    var successUrl = $(this).closest('a').attr('rel');
    if (successUrl == 'nofollow')
      successUrl = undefined;
    var ajaxOptions = {
      type: 'POST',
      url: actionUrl,
      data: typeof (successUrl) == 'undefined' ? {} : { successUrl: successUrl },
      dataType: 'json',
      //complete: function() { window.locationFake = this.url; },
      success: function(result) {
        var tmpFn = function() {
          if (result.hasError == false && typeof (result.successUrl) == 'string')
            window.location = result.successUrl;
          else if (result.hasError == true && typeof (result.errorUrl) == 'string')
            window.location = result.errorUrl;
        };
        if (typeof (result.message) == 'string') {
          messageBox(result.message, result.hasError ? 'Errore' : 'Messaggio', tmpFn);
        }
        else {
          tmpFn();
        }
        //
      }
    };
    if (typeof (confirm) == 'undefined')
      $.ajax(ajaxOptions);
    else
      messageBox2(confirm, 'Avviso', function() { $.ajax(ajaxOptions); }, null);
  });
  //
  $('.formSubmitWithAjaxLoad').live('submit click change', function(e) {
    //
    var containerSelector = '.ajaxLoadContainer';
    var container = $(this).closest(containerSelector);
    var form = $(this).closest('form');
    if (container.length == 0)
      return true;
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    var meta = $.extend(form.metadata(), container.metadata());
    var data = {};
    $(this).closest('form').ajaxSubmit({
      type: 'POST',
      data: data,
      //dataType: 'json',
      //complete: function() { window.locationFake = this.url; },
      success: function(result) {
        var url = window.locationFake || window.location.toString();
        container.load(url + ' ' + containerSelector, function() {
          window.locationFake = url;
          if (typeof (meta.ajaxLoadCallback) == 'function') {
            meta.ajaxLoadCallback.call(form, container);
          }
        });
        //
      }
    });
    return false;
  });
  //
  $('.opendialogHijack').live('click', function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    var wdg = $(this);
    var url = wdg.attr('rel');  // load remote content from its 'rel' attribute into hidden div
    if ((url || '').length == 0 || url == 'nofollow')
      url = wdg.attr('href');
    //configurabile con il meta plugin
    $('<div></div>').load(url, function() {
      var dlg = $(this);
      try { dlg.dialog('destroy'); } catch (ex) { }
      dlg.dialog($.extend({
        title: wdg.attr('title') || document.title,
        modal: true,
        width: 500,
        open: function() {
          ajaxLoadPostProcessor(this, wdg.metadata().hijack || true);
          if (typeof (wdg.metadata().ajaxLoadCallback) == 'function') {
            wdg.metadata().ajaxLoadCallback.call(dlg);
          }
        },
        close: function() { dlg.dialog('destroy'); dlg.remove(); }
      }, wdg.metadata()));
    });
  });
  //--- widget documenti ---
  $('ul.autoExpand').each(function() {
    $('li.section >ul', this).hide();
    $('li.section >:not(ul)', this).bind('click', function(e) {
      //if ($('>ul', $(this).closest('li')).length > 0 && $('>ul:visible', $(this).closest('li')).length == 0)
      //  e.preventDefault();
      //  e.stopPropagation();
      //}
      $('>ul', $(this).closest('li')).toggle('fast');
    });
  });
  //
  $('a.autoConfirm').live('click', function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
    } catch (ex) { }
    var lk = $(this).attr('href');
    messageBox2($(this).attr('autoConfirm'), 'Avviso', function() { window.location = lk; }, null);
  });
  //
});


var ajaxLoadPostProcessor = function(block, runHijack) {
  var blockProcessorHandler = function() {
    $('.buttonStyle', this).button();
    $('.autoDatePicker', this).each(function() { $(this).datepicker($.extend({ dateFormat: 'yy-mm-dd' }, $(this).metadata())); });
    // per utilizzare fckeditor usare da qualche parte nei controller la chiamata preventiva per inizializzare il VPP: Gizmox.WebGUI.Forms.Editors.CKEditorSupport.RegisteVPP();
    $('textarea.fckEditor', this).fck($.extend({ path: '/Author/FCKeditor_Author/', toolbar: 'Ikon' }, $(this).metadata()));
    $('.autoWatermark', this).each(function(i) { $(this).watermark($(this).metadata().watermark || $(this).attr('watermark')); });
    //$('.autoWatermark', this).each(function(i) { $(this).watermark($(this).attr('watermark')); });
    //
    var isCompatibleWithNoscript = true;
    // per nascondere automaticamente i blocchi di testo vuoti, con solo spazi o BR
    if (isCompatibleWithNoscript) {
      $('.autoHide').filter(function() { return $.trim($(this).text()).length == 0; }).hide();
      $('.autoHideIfNoTags:not(>*)').hide();
    }
    else {
      $('.autoHide').filter(function() { return $.trim($(this).text()).length > 0; }).show();
      $('.autoHideIfNoTags:has(>*)').show();
    }
    $('.autoShow').show();
    $('.autoHideParent').filter(function() { return $.trim($(this).text()).length == 0; }).parent().hide();
    //
    $('form.autoDisableInputs').submit(function(e) {
      //per disabilitare il submit via get dei filtri nulli
      $('input:not(:checkbox),select,:checked', this).each(function(i) {
        if ($(this).val().length == 0)
          $(this).attr("disabled", "disabled");
      });
    });
    //
  };
  try {
    blockProcessorHandler.call(block);
    if (runHijack === true)
      $(block).hijack(blockProcessorHandler);
  } catch (ex) { }
}


var messageBox = function(messageHtml, title, callbackOK) {
  var markup = $('<div></div>').html(messageHtml);
  title = title || window.document.title;
  markup.dialog({
    autoOpen: true,
    title: title,
    bgiframe: true,
    modal: true,
    zIndex: 10000,
    closeText: 'Chiudi',
    close: function() { $(this).dialog('destroy') },
    buttons: {
      Ok: function() {
        if (callbackOK && typeof (callbackOK) === "function")
          callbackOK.call($(this));
        $(this).dialog('destroy');
      }
    }
  });  //dialog
}  //messageBox


var messageBox2 = function(messageHtml, title, callbackOK, callbackCancel) {
  var markup = $('<div></div>').html(messageHtml);
  title = title || window.document.title;
  markup.dialog({
    autoOpen: true,
    title: title,
    bgiframe: true,
    modal: true,
    zIndex: 10000,
    closeText: 'Chiudi',
    close: function() { $(this).dialog('destroy') },
    buttons: {
      Ok: function() {
        if (callbackOK && typeof (callbackOK) === "function")
          callbackOK.call($(this));
        $(this).dialog('destroy');
      },
      Annulla: function() {
        if (callbackCancel && typeof (callbackCancel) === "function")
          callbackCancel.call($(this));
        $(this).dialog('destroy');
      }
    }
  });  //dialog
}  //messageBox2


var messageBoxUrl = function(urlToLoad, title, _width, _height) {
  title = title || window.document.title;
  //$popup.dialog('option', 'width', $(popup).parent().width());
  var auxFrame = $('<div />').load(urlToLoad, null, function() {
    var wdg = $(this);
    $(this).dialog({
      autoOpen: true,
      title: title,
      bgiframe: true,
      height: _height || 'auto',
      width: _width || 'auto',
      modal: true,
      zIndex: 10000,
      closeText: 'Chiudi',
      close: function() { wdg.dialog('destroy') }
    });
  });
} //messageBoxUrl


//
// ajax loader
//
//http://malsup.com/jquery/block/#options
var ajaxLoader = function(delay, imageSrc, imageSize, options) {
  var timoutHandler = null;
  var timoutDepth = 0;
  if ((imageSrc || '').length == 0)
    return;
  imageSize = imageSize || 32;
  //
  $('body').ajaxStart(function() {
    timoutDepth++;
    if (timoutHandler == null)
      timoutHandler = setTimeout(function() {
        timoutHandler = null;
        var optionsCurrent = $.extend({
          fadeIn: 250,
          fadeOut: 0,
          css: { border: '0px', top: '40%', width: 'auto', left: (($(window).width() - imageSize) / 2) },
          showOverlay: true,
          overlayCSS: { backgroundColor: '#000', opacity: 0.3 },
          message: '<img src="' + imageSrc + '"/>'
        }, options);
        $.blockUI(optionsCurrent);
      }, delay);
  }).ajaxStop(function() {
    timoutDepth--;
    if (timoutDepth <= 0) {
      if (timoutHandler != null) {
        clearTimeout(timoutHandler);
        timoutHandler = null;
      }
      $.unblockUI();
    }
  });
  //
};


//
// base64 encoding/decoding support
//
var keyStr_base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";

function EncodeBase64(input) {
  var output = "";
  var chr1, chr2, chr3, enc1, enc2, enc3, enc4;
  var i = 0;
  var uTF8Encode = function(string) {
    string = string.replace(/\x0d\x0a/g, "\x0a");
    var output = "";
    for (var n = 0; n < string.length; n++) {
      var c = string.charCodeAt(n);
      if (c < 128) {
        output += String.fromCharCode(c);
      } else if ((c > 127) && (c < 2048)) {
        output += String.fromCharCode((c >> 6) | 192);
        output += String.fromCharCode((c & 63) | 128);
      } else {
        output += String.fromCharCode((c >> 12) | 224);
        output += String.fromCharCode(((c >> 6) & 63) | 128);
        output += String.fromCharCode((c & 63) | 128);
      }
    }
    return output;
  };
  input = uTF8Encode(input);
  while (i < input.length) {
    chr1 = input.charCodeAt(i++);
    chr2 = input.charCodeAt(i++);
    chr3 = input.charCodeAt(i++);
    enc1 = chr1 >> 2;
    enc2 = ((chr1 & 3) << 4) | (chr2 >> 4);
    enc3 = ((chr2 & 15) << 2) | (chr3 >> 6);
    enc4 = chr3 & 63;
    if (isNaN(chr2)) {
      enc3 = enc4 = 64;
    } else if (isNaN(chr3)) {
      enc4 = 64;
    }
    output = output + keyStr_base64.charAt(enc1) + keyStr_base64.charAt(enc2) + keyStr_base64.charAt(enc3) + keyStr_base64.charAt(enc4);
  }
  return output;
}


function DecodeBase64(input) {
  var output = "";
  var chr1, chr2, chr3;
  var enc1, enc2, enc3, enc4;
  var i = 0;
  input = input.replace(/[^A-Za-z0-9\+\/\=]/g, "");
  while (i < input.length) {
    enc1 = keyStr_base64.indexOf(input.charAt(i++));
    enc2 = keyStr_base64.indexOf(input.charAt(i++));
    enc3 = keyStr_base64.indexOf(input.charAt(i++));
    enc4 = keyStr_base64.indexOf(input.charAt(i++));
    chr1 = (enc1 << 2) | (enc2 >> 4);
    chr2 = ((enc2 & 15) << 4) | (enc3 >> 2);
    chr3 = ((enc3 & 3) << 6) | enc4;
    output = output + String.fromCharCode(chr1);
    if (enc3 != 64) {
      output = output + String.fromCharCode(chr2);
    }
    if (enc4 != 64) {
      output = output + String.fromCharCode(chr3);
    }
  }
  var uTF8Decode = function(input) {
    var string = "";
    var i = 0;
    var c = c1 = c2 = 0;
    while (i < input.length) {
      c = input.charCodeAt(i);
      if (c < 128) {
        string += String.fromCharCode(c);
        i++;
      } else if ((c > 191) && (c < 224)) {
        c2 = input.charCodeAt(i + 1);
        string += String.fromCharCode(((c & 31) << 6) | (c2 & 63));
        i += 2;
      } else {
        c2 = input.charCodeAt(i + 1);
        c3 = input.charCodeAt(i + 2);
        string += String.fromCharCode(((c & 15) << 12) | ((c2 & 63) << 6) | (c3 & 63));
        i += 3;
      }
    }
    return string;
  };
  output = uTF8Decode(output);
  return output;
}

