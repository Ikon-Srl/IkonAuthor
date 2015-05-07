/*
*
* Copyright (c) 2009 Ikon srl, by Marco Venuti
* 
*/

(function($) {
  /*
  * A basic news vertical scroller.
  *
  * @param    delay      Delay (in milliseconds) between iterations. Default 4 seconds (5000ms)
  * @example  $(".news_scroller").newsScroller(5000);
  *
  * per poter calcolare correttamente l'altezza dell'elemento da scrollare
  * usare per gli items:  clear: both; float: left;
  *
  */
  $.fn.newsScroller = function(minItems, delay, speed, autoHide, dirVertical, OnScrollStart, OnScrollEnd) {
    minItems = minItems || 1 << 30;  // if less than minItems don't start the animation
    delay = delay || 5000;
    speed = speed || 250;
    autoHide = typeof autoHide === "boolean" ? autoHide : true;
    dirVertical = typeof dirVertical === "boolean" ? dirVertical : true;
    var initScroller = function(el) {
      stopScroller(el);
      el.items = $('>*', el);
      el.items.show();
      if (el.items.length <= minItems)
        return;
      else {
        if (autoHide)
          el.items.slice(minItems).hide();
      }
      startScroller(el);
    };
    var startScroller = function(el) {
      el.scrollitemfn = setInterval(function() { doScrollItem(el) }, delay)
    };
    var stopScroller = function(el) {
      clearInterval(el.scrollitemfn);
    };
    var pauseScroller = function(el) {
      el.pause = true;
    };
    var resumeScroller = function(el) {
      el.pause = false;
    };
    var doScrollItem = function(el) {
      if (el.pause || el.pauseAnim || el.items.length <= minItems)
        return;
      // pause until animation has finished
      el.pauseAnim = true;
      el.items = $(el.items.selector, el.items.context);
      try {
        if (OnScrollStart && typeof (OnScrollStart) === "function")
          OnScrollStart.call(this, el.items[0]);
      }
      catch (e) { }
      var itm1 = $(el.items[0]);
      var itm2 = itm1.clone(true);
      if (autoHide)
        $(el.items[minItems]).show();
      itm1.animate(dirVertical ? { marginTop: -itm1.height() + 'px'} : { marginLeft: -itm1.width() + 'px' }, speed, function() {
        itm1.remove();
        if (autoHide) {
          itm1.hide();
          itm2.hide();
        }
        $(el.items.context).append(itm2);
        el.pauseAnim = false;
        try {
          if (OnScrollEnd && typeof (OnScrollEnd) === "function")
            OnScrollEnd.call(this, el.items[0], itm2);
        }
        catch (e) { }
      });
    };
    //
    this.each(function() {
      initScroller(this);
      $(this).hover(function() { pauseScroller(this); }, function() { resumeScroller(this); });
    });
    return this;
  };

})(jQuery);
