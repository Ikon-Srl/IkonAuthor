; (function($) {
  $.fn.captcha = function(options) {

    var defaults = {
      borderColor: '',
      captchaDir: '/Content/css/captcha',
      url: '/Auth/CaptchaCode',
      text: "Trascina l'icona <span>scissors</span><br/>all'interno del cerchio.",
      items: Array("pencil", "scissors", "clock", "heart", "note"),
      itemsTxt: Array("matita", "forbici", "orologio", "cuore", "nota")
    };

    var options = $.extend(defaults, options);

    $(this).html("<b class='ajax-fc-rtop'><b class='ajax-fc-r1'></b> <b class='ajax-fc-r2'></b> <b class='ajax-fc-r3'></b> <b class='ajax-fc-r4'></b></b><img class='ajax-fc-border' id='ajax-fc-left' src='" + options.captchaDir + "/imgs/border-left.png' /><img class='ajax-fc-border' id='ajax-fc-right' src='" + options.captchaDir + "/imgs/border-right.png' /><div id='ajax-fc-content'><div id='ajax-fc-left'><p id='ajax-fc-task'>" + options.text + "</p><ul id='ajax-fc-task'><li class='ajax-fc-0'><img src='" + options.captchaDir + "/imgs/item-none.png' alt='' /></li><li class='ajax-fc-1'><img src='" + options.captchaDir + "/imgs/item-none.png' alt='' /></li><li class='ajax-fc-2'><img src='" + options.captchaDir + "/imgs/item-none.png' alt='' /></li><li class='ajax-fc-3'><img src='" + options.captchaDir + "/imgs/item-none.png' alt='' /></li><li class='ajax-fc-4'><img src='" + options.captchaDir + "/imgs/item-none.png' alt='' /></li></ul></div><div id='ajax-fc-right'><p id='ajax-fc-circle'></p></div></div><div id='ajax-fc-corner-spacer'></div><b class='ajax-fc-rbottom'><b class='ajax-fc-r4'></b> <b class='ajax-fc-r3'></b> <b class='ajax-fc-r2'></b> <b class='ajax-fc-r1'></b></b>");
    $.ajax({ url: options.url, cache: false, async: false, success: function(result) {
      var rand = result;
      var pic = randomNumber();
      $(".ajax-fc-" + rand).html("<img src=\"" + options.captchaDir + "/imgs/item-" + options.items[pic] + ".png\" alt=\"\" />");
      $("p#ajax-fc-task span").html(options.itemsTxt[pic]);
      $(".ajax-fc-" + rand).addClass('ajax-fc-highlighted');
      $(".ajax-fc-" + rand).draggable({ containment: '#ajax-fc-content' });
      var used = Array();
      for (var i = 0; i < 5; i++) {
        if (i != rand && i != pic) {
          $(".ajax-fc-" + i).html("<img src=\"" + options.captchaDir + "/imgs/item-" + options.items[i] + ".png\" alt=\"\" />");
          used[i] = options.items[i];
        }
      }
      $(".ajax-fc-container, .ajax-fc-rtop *, .ajax-fc-rbottom *").css("background-color", options.borderColor);
      $("#ajax-fc-circle").droppable({
        drop: function(event, ui) {
          $(".ajax-fc-" + rand).draggable("disable");
          $(this).closest('form').append("<input type=\"hidden\" style=\"display: none;\" name=\"captcha\" value=\"" + rand + "\">");
        },
        tolerance: 'touch'
      });
    }
    });
    //
    function randomNumber() {
      var chars = "01234";
      chars += ".";
      var size = 1;
      var i = 1;
      var ret = "";
      while (i <= size) {
        var $max = chars.length - 1;
        var $num = Math.floor(Math.random() * $max);
        var $temp = chars.substr($num, 1);
        ret += $temp;
        i++;
      }
      return ret;
    }
    //
  };

})(jQuery);

