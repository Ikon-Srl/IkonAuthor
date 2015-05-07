$(function(){
var openmenu=null;
$("#menu ul.level_2").addClass("open").hide();  //prepara per il primo passaggio di mouse
$("#menu .top-menu>li:has('ul.level_2')").hoverIntent(
    function(){if(openmenu!=null && openmenu!=this)$("ul.level_2",openmenu).hide();$("ul.level_2",this).show();openmenu=this;},
    function(){$("ul.level_2",this).hide();});
});