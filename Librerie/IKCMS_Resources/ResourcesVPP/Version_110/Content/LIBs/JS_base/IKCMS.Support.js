//
// support functions to be defined before $(document).ready
//

var ajaxLoadPostProcessor = function(block, runHijack) {
  var blockProcessorHandler = function() {
    try {
      var that = this;
      // usare la data-api di bootstrap impiegando gli attributi:
      // data-provide="datepicker" data-date-format="yyyy-mm-dd"
      //$('.autoDatePicker').datepicker();  // data-date-format="yyyy-mm-dd"
      //
      // per nascondere automaticamente i blocchi di testo vuoti, con solo spazi o BR
      var isCompatibleWithNoscript = true;
      if (isCompatibleWithNoscript) {
        $('.autoHide').filter(function() { return $.trim($(this).text()).length == 0; }).hide();
        $('.autoHideIfNoTags:not(>*)').hide();
      }
      else {
        $('.autoHide').filter(function() { return $.trim($(this).text()).length > 0; }).show();
        $('.autoHideIfNoTags:has(>*)').show();
      }
      //
      $('.autoShow').show();
      $('.autoHideParent').filter(function() { return $.trim($(this).text()).length == 0; }).parent().hide();
      //
      if ($.fn.dotdotdot) {
        $('.ellipsis-block,.ellipsis-live').each(function() { var block = $(this); block.dotdotdot({ watch: block.hasClass('ellipsis-live') }); })
      }
      //
      // per utilizzare fckeditor usare da qualche parte nei controller la chiamata preventiva per inizializzare il VPP: Gizmox.WebGUI.Forms.Editors.CKEditorSupport.RegisteVPP();
      if ($.fck) {
        setTimeout(function() { $('textarea.fckEditor', that).fck($.extend({ path: GetSiteBaseUrl() + 'Author/FCKeditor_Author/', toolbar: 'Ikon' }, $(that).metadata())); }, 0);
      }
      //$('.autoWatermark', this).each(function(i) { $(this).watermark($(this).metadata().watermark || $(this).attr('watermark')); });  //utilizzare l'attributo html5 placeholder="..."
      //
    } catch (ex) { }
  };
  try {
    blockProcessorHandler.call(block);
    if (runHijack === true)
      $(block).hijack(blockProcessorHandler);
  } catch (ex) { }
}


var ajaxLoadInitializer_GA = function() {
  try { _gaq.push(['_trackPageview', window.locationFake || window.location]); } catch (e) { }  //GA tracking for ajax contents
};


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


var messageBox = function(messageHtml, callbackOK) { if (callbackOK && typeof (callbackOK) === "function") { return bootbox.alert(messageHtml, callbackOK); } else { return bootbox.alert(messageHtml); } };


var GetSiteBaseUrl = function() { return $('body').attr('data-baseUrl') || $('body').attr('baseUrl') || $('base').attr('href') || '/'; };


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

//
// standard javascript helpers
//
$(document).ready(function() {
    //
    //try { $(document).pngFix(); } catch (ex) { }  //trasparenza png per IE6
    //
    ajaxLoadPostProcessor(document, false);
    //
    $.ajaxSetup({ timeout: parseInt($('#ajaxTimeOut').val() || '120000') });  // per eseguire l'override dei timeout ajax inserire nella pagina il codice <input type="hidden" id="ajaxTimeOut" name="ajaxTimeOut" value="3600000" />
    //$.ajaxSetup({ timeout: 3600000 });
    //
    $(document).on('change', '.autoSubmitDDL,.autoSubmitCB,.autoSubmitTB', function(e) { if ($.browser.msie) { $('iframe').remove(); } $(this).closest('form').submit(); });  // risolve i problemi con IE <= 7 e presenza di iframe con src su domini esterni
    //
    $.hrefProcessorHandlerGetUrl = function(link, url) {
        if (url == null || typeof (url) == 'undefined') url = link.attr('href');
        // aggiunge dinamicamente la querystring/hash UrlBack
        if (link.hasClass('append_UrlBack')) {
            try {
                //var UrlBack = $.param.querystring(window.locationFake || window.location.toString(), { UrlBack: null }, 0);
                var UrlBack = (window.locationFake || window.location.toString()).replace(/(\?|&)UrlBack=.*(&|$)/gi, '');
                url = $.param.querystring(url, { UrlBack: EncodeBase64(UrlBack) }, 0);
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
        return url;
    };
    //
    $.hrefProcessorHandler = function(link, e) {
        var url = $.hrefProcessorHandlerGetUrl(link, null);
        if (link.hasClass('ajaxUpdate')) {
            //try { e.stopImmediatePropagation(); } catch (ex) { }  // per interrompere gli altri event handlers altrimenti ajaxUpdateHandler viene chiamata 2 volte
            //return $.ajaxUpdateHandler.call(link, e);
            return true;
        }
        try {
            e.preventDefault();
            e.stopPropagation();
        } catch (ex) { }
        if ((url || '').length > 0) {
            var target = link.attr('target') || '';
            try { if (e.ctrlKey) target = '_blank'; } catch (ex) { }
            if (target.length > 0) {
                window.open(url, target);
            }
            else {
                window.location = url;
            }
        }
        return false;
    };
    //
    // clickableBlock management
    //
    if ($.browser.msie && parseInt($.browser.version, 10) <= 8) {
        $(document).on({
            click: function(e) { return $.hrefProcessorHandler($(this).find('a.clickableBlockLink'), e); }
        }, '.clickableBlock:has(a.clickableBlockLink)');
    } else {
        $(document).on({
            click: function(e) { return $.hrefProcessorHandler($(this).find('a.clickableBlockLink'), e); },
            mouseenter: function(e) { $(this).addClass('hover'); },
            mouseleave: function(e) { $(this).removeClass('hover'); }
        }, '.clickableBlock:has(a.clickableBlockLink)');
    }
    //
    $(document).on('click', 'a.append_UrlBack:not(.clickableBlockLink)', function(e) {
        return $.hrefProcessorHandler($(this), e);
    });
    //
    // per la gestione della url di ritorno nei moduli di autentifica
    $(document).on('click', 'a.append_ReturnUrl', function(e) {
        return $.hrefProcessorHandler($(this), e);
    });
    //
    // parsing robusto dei bool il primo argomento e' il valore di default, poi si passa il resto degli argomenti con parametri variabili
    //
    $.tryParseBool = function(defautlValue) {
        if (typeof (defautlValue) != 'boolean') {
            defautlValue = false;
        }
        var result = null;
        for (var i = 1; i < arguments.length; i++) {
            try {
                var value = arguments[i]
                if (typeof (value) == 'boolean') {
                    if (value === true) return true;
                    result = false;
                    continue;
                }
                else if (typeof (value) == 'number') {
                    if (value === 0) {
                        result = false;
                        continue;
                    }
                    return true;
                }
                if (typeof (value) == 'string' && value === '0') {
                    result = false;
                    continue;
                }
                if (value != null && typeof (value) != 'undefined') {
                    if (/^\s*(true|on|yes|ok|1)\s*$|/i.test(value.toString())) return true;
                    result = false;
                    continue;
                }
            }
            catch (ex) { }
        }
        if (typeof (result) == 'boolean')
            return result;
        return defautlValue;
    }
    //
    $.getFunctionByName = function(functionName, context) {
        try {
            context = context || window;
            var namespaces = functionName.split(".");
            var func = namespaces.pop();
            for (var i = 0; i < namespaces.length; i++) {
                context = context[namespaces[i]];
            }
            return context[func];
        }
        catch (ex) { }
        return function() { };
    }
    //
    $(document).on('submit', 'form.autoDisableInputs', function(e) {
        //per disabilitare il submit via get dei filtri nulli
        $('input:not(:checkbox),select,:checked', this).each(function(i) {
            if ($(this).val().length == 0)
                $(this).attr("disabled", "disabled");
        });
    });
    //
    // extended ajax history management for ajaxUpdate
    $(window).on('popstate', function(event) {
        try {
            var status = event.state;
            if (typeof (status) == 'undefined' || status == null)
                return true;
            if (status.type == 'ajaxUpdate' && status.url.length > 0) {
                window.location = status.url;
                return false;
            }
        } catch (ex) { }
    });
    //
    // extended ajax loading support
    //
    $(document).on('submit', '.ajaxUpdateForm', function(e) { return $.ajaxUpdateHandler.call(this, e); });
    $(document).on('click', '.ajaxUpdate,button.ajaxUpdateForm', function(e) { return $.ajaxUpdateHandler.call(this, e); });
    $.ajaxUpdateHandler = function(e) {
        //
        var cfg = {};
        cfg.containerSelector = '.ajaxUpdateContainer';
        cfg.datablockattr = 'ajaxupdateblock';  // seleziona gli elementi con l'attributo data-ajaxupdateblock=''
        cfg.updatesSelector = 'update[selector]';  // seleziona gli elementi tipo <update selector=''>...</update>
        cfg.target = $(this);
        cfg.container = $(this).closest(cfg.containerSelector);
        //
        if (cfg.target.length == 0 || cfg.container.length == 0) {
            return true;
        }
        //
        try {
            e.preventDefault();
            e.stopPropagation();
            //e.stopImmediatePropagation();
        } catch (ex) { }
        //
        try {
            if (e.ctrlKey) {
                var url = $.hrefProcessorHandlerGetUrl(cfg.target, null);
                if (url.length > 0) {
                    window.open(url, '_blank');
                    return false;
                }
            }
        } catch (ex) { }
        //
        cfg.callbacks = (cfg.target.data('callback') || '').split(',').concat((cfg.container.data('callback') || '').split(','));
        cfg.viewcode = cfg.target.data('viewcode') || cfg.container.data('viewcode') || '';
        cfg.viewcodepartial = cfg.target.data('viewcodepartial') || cfg.container.data('viewcodepartial') || '';
        //
        cfg.ajaxcaching = $.tryParseBool(true, cfg.target.data('ajaxcaching'), cfg.container.data('ajaxcaching'));
        cfg.filterscripts = $.tryParseBool(true, cfg.target.data('filterscripts'), cfg.container.data('filterscripts'));
        cfg.containerreadonly = $.tryParseBool(true, cfg.container.data('containerreadonly'));
        cfg.updatebrowserurl = $.tryParseBool(true, cfg.target.data('updatebrowserurl'), cfg.container.data('updatebrowserurl'));
        //
        var ajaxData = {};
        if (cfg.viewcodepartial.length > 0) {
            ajaxData.PartialViewTemplateCode = cfg.viewcodepartial;
        }
        else if (cfg.viewcode.length > 0) {
            ajaxData.ViewTemplateCode = cfg.viewcode;
        }
        //
        var responseHandler = function(response) {
            // to avoid any 'Permission Denied' errors in IE
            if (cfg.filterscripts) { response = response.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, ''); }
            var resultsDOM = $("<div></div>").append(response);
            var updateBlocks = resultsDOM.find('[data-' + cfg.datablockattr + ']');
            if (updateBlocks.length > 0) {
                updateBlocks.each(function(i) {
                    var panel = $(this);
                    var target = $('[data-' + cfg.datablockattr + '="' + panel.data('ajaxupdateblock') + '"]');
                    target.html(panel.html());
                    try { ajaxLoadPostProcessor(target, false); } catch (ex) { }
                    // callbacks custom per ciascun panel
                    var callbacks = (panel.data('callback') || '').split(',');
                    for (var i = 0; i < callbacks.length; i++) {
                        if (callbacks[i].length > 0) {
                            var callback = $.getFunctionByName(callbacks[i]);
                            if (typeof (callback) == 'function') {
                                callback.call(target);
                            }
                        }
                    }
                });
            }
            else if ((updateBlocks = resultsDOM.find(cfg.updatesSelector)).length > 0) {
                updateBlocks.each(function(i) {
                    var panel = $(this);
                    var selector = panel.attr('selector');
                    var target = $(selector);
                    target.html(panel.html());
                    try { ajaxLoadPostProcessor(target, false); } catch (ex) { }
                    // callbacks custom per ciascun panel
                    var callbacks = (panel.data('callback') || '').split(',');
                    for (var i = 0; i < callbacks.length; i++) {
                        if (callbacks[i].length > 0) {
                            var callback = $.getFunctionByName(callbacks[i]);
                            if (typeof (callback) == 'function') {
                                callback.call(target);
                            }
                        }
                    }
                });
            }
            else {
                if (cfg.containerreadonly) {
                    cfg.container.html(resultsDOM.find(cfg.containerSelector).html());  // mantiene il cfg.containerSelector originale
                }
                else {
                    cfg.container.replaceWith(resultsDOM.find(cfg.containerSelector));  // sostituisce anche cfg.containerSelector
                }
                try { ajaxLoadPostProcessor(cfg.container, false); } catch (ex) { }
            }
            for (var i = 0; i < cfg.callbacks.length; i++) {
                if (cfg.callbacks[i].length > 0) {
                    var callback = $.getFunctionByName(cfg.callbacks[i]);
                    if (typeof (callback) == 'function') {
                        callback.call(cfg.target, response);
                    }
                }
            }
        };
        //
        var url = window.locationFake || window.location;
        //
        if (cfg.updatebrowserurl) {
            try {
                var mode = null;
                try { mode = window.history.state.type; } catch (ex) { }
                if (mode != 'ajaxUpdate') window.history.replaceState({ type: 'ajaxUpdate', url: url.toString() }, window.document.title, url.toString());
            } catch (ex) { }
        }
        //
        if (cfg.target.hasClass('ajaxUpdateForm')) {
            var form = cfg.target.closest('form');
            var url_base = form.attr('action') || url;
            var qs = form.serialize();
            url = $.param.querystring(url_base, qs, 2);
        }
        else {
            var url = $.hrefProcessorHandlerGetUrl(cfg.target, null);
        }
        url = url.replace(/^#*/gi, '');  //sostituzione delle url farlocche per bloccare i BOTs
        //
        $.ajax({
            type: 'POST',
            url: url,
            cache: cfg.ajaxcaching,
            data: ajaxData,  // questi parametri dinamici vengono passati via post per non alterare la url della pagina!
            dataType: 'html',
            complete: function() {
                window.locationFake = this.url;
                if (cfg.updatebrowserurl) { try { window.history.pushState({ type: 'ajaxUpdate', url: window.locationFake, ajaxData: ajaxData }, window.document.title, window.locationFake); } catch (ex) { } }
                ajaxLoadInitializer_GA();
            },
            success: responseHandler
        });
        return false;
    };
    //
    // json forms management functions
    //
    $.formSubmitWithJsonReturnHandler = function(e) {
        try {
            e.preventDefault();
            e.stopPropagation();
        } catch (ex) { }
        //
        var link = $(this);
        if (!link.hasClass("ajaxSubmit_disabled")) {
            link.addClass("ajaxSubmit_disabled");
            var data = {};
            if (($('input[name="successUrl"]', $(this).closest('form')).val() || '').length == 0)
                data.successUrl = $(this).closest('a').attr('href') || document.referrer;
            $(this).closest('form').ajaxSubmit({
                //beforeSubmit: function() { alert('beforeSubmit'); },
                type: 'POST',
                data: data,
                dataType: 'json',
                complete: function() { window.locationFake = this.url; },
                success: function(result) {
                    link.removeClass("ajaxSubmit_disabled")
                    var tmpFn = function() {
                        if (result.hasError == false && typeof (result.successUrl) == 'string')
                            window.location = result.successUrl;
                        else if (result.hasError == true && typeof (result.errorUrl) == 'string')
                            window.location = result.errorUrl;
                    };
                    if (typeof (result.message) == 'string') {
                        bootbox.alert(result.message, tmpFn);
                    }
                    else {
                        tmpFn();
                    }
                    //
                }
            });
        }
    };
    //
    $('.formSubmitWithJsonReturn').unbind('click').click(function(e) { $.formSubmitWithJsonReturnHandler.call(this, e); return false; });
    //
    $(document).on('click', '.formSubmitWithJsonReturnLive', function(e) { $.formSubmitWithJsonReturnHandler.call(this, e); return false; });
    //
    // json links management functions
    //
    $(document).on('click', '.linkWithJsonReturn', function(e) {
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
            complete: function() { window.locationFake = this.url; },
            success: function(result) {
                var tmpFn = function() {
                    if (result.hasError == false && typeof (result.successUrl) == 'string')
                        window.location = result.successUrl;
                    else if (result.hasError == true && typeof (result.errorUrl) == 'string')
                        window.location = result.errorUrl;
                };
                if (typeof (result.message) == 'string') {
                    bootbox.alert(result.message, tmpFn);
                }
                else {
                    tmpFn();
                }
                //
            }
        };
        if (typeof (confirm) == 'undefined')
            $.ajax(ajaxOptions);
        else {
            bootbox.confirm(confirm, function(result) { if (result) { $.ajax(ajaxOptions); } });
        }
    });
    //
    $(document).on('click', '.opendialogHijack', function(e) {
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
    // functionBotNoFollow equivalent
    $(document).on('click', 'a[rel~="nofollow"][href^="#"]', function(e) {
        $(this).attr('href', $(this).attr('href').substr(1));
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
    $(document).on('click', 'a.autoConfirm', function(e) {
        try {
            e.preventDefault();
            e.stopPropagation();
        } catch (ex) { }
        var lk = $(this).attr('href');
        bootbox.confirm($(this).data('autoconfirm') || $(this).attr('autoconfirm'), function(result) { if (result) { window.location = lk; } });
    });
    //
    // default options for fancybox
    // per caricare contenuti ajax aggiungere al link: data-fancybox-type='ajax'
    // non utilizzare mai piu' chiamate a .fancybox( perche' altrimenti comanda solo l'ultima anche se su selettori diversi
    $.fancyboxDefaultOptions = { live: true, padding: 10, helpers: { title: { type: 'inside' }, overlay: { css: { 'background': 'rgba(0,0,0, 0.7)'}}} };
    //try { $('a.autoPopupAny,a.autoPopup:has(img)').fancybox($.extend($.fancyboxDefaultOptions, { padding: 0 })); } catch (e) { }
    //
});

