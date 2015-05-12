
$(document).ready(function() {
  //
  // fancybox initializer
  // per caricare contenuti ajax aggiungere al link: data-fancybox-type='ajax'
  try { $('a.autoPopupAny,a.autoPopup:has(img)').fancybox($.extend($.fancyboxDefaultOptions, { padding: 10 })); } catch (e) { }
  //
  $('a.autoPopupVideo').fancybox();
  $('.teaser_rotator').each(function(i) {
    var teaser = $(this);
    var cfg = $.extend({ minItems: 1, delay: 10000, speed: 1000, autoHide: true, dirVertical: true, overlapAnimationsSelector: '.animationGroup' }, $(this).metadata());
    setTimeout(function() { teaser.MV_TeaserRotator(cfg.minItems, cfg.delay, cfg.speed, cfg.autoHide, cfg.dirVertical, cfg.overlapAnimationsSelector); }, i * 1500);
  });
  //
  $('.teaser_fade').each(function(i) {
    var teaser = $(this);
    var cfg = $.extend({ timeout: 10000, speed: 250, fx: 'fade' }, $(this).metadata());
    setTimeout(function() { teaser.cycle(cfg); }, i * 1500);
  });
  //
  // supporto per colonne automatiche con config in metadata
  //$('.autocolumn').each(function() { $(this).columnize($.extend({}, $(this).metadata())); });
  //
  // image processing
  //$('img.autoScale').scaleImage({ scale: 'fit', center: true });
  //$('img.autoCrop').scaleImage({ scale: 'fill', center: true });
  //
  /*
  $('.htmlTruncate').condense({
  condensedLength: 200,
  minTrail: 20,
  moreSpeed: 'fast',
  lessSpeed: 'slow',
  moreText: '<span>espandi &raquo;</span>',  // wrapped in span .condense_control .condense_control_more
  lessText: '<span>&laquo; compatta</span>',  // wrapped in span .condense_control .condense_control_less
  ellipsis: '&hellip;'
  });
  //
  $('.htmlTeaserTruncate').condense({
  condensedLength: 40,
  minTrail: 10,
  moreText: '',  // wrapped in span .condense_control .condense_control_more
  lessText: '',  // wrapped in span .condense_control .condense_control_less
  ellipsis: '&hellip;'
  });
  */
  //
});


$(window).load(function() {
  try {
    //
    // colonne di uguale altezza (non usarlo in ready per dare tempo al resto dei plugins di operare)
    //$('#altezza').equalHeights();
    //$('.equal').equalHeights();
    //
  } catch (e) { }
});

