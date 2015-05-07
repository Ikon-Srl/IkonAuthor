/**
 * jQuery hijack plugin
 *
 * Plugin allows you to make links and forms within a remotely loaded content 
 * change only the container they have been loaded in (a tab, dialog or any other kind of widget) 
 * in an easy way.
 *
 * Dual licensed under the MIT and GPL licenses:
 *   http://www.opensource.org/licenses/mit-license.php
 *   http://www.gnu.org/licenses/gpl.html
 *
 * @version $Id: jquery.hijack.js 4 2009-03-10 00:15:33Z kkotowicz $
 * @author Klaus Hartl (initial idea)
 * @author modified by Krzysztof Kotowicz <kkotowicz at gmail dot com>
 * @see http://groups.google.com/group/jquery-ui/browse_thread/thread/c728b8464f669674/711a7b0dd4236190?lnk=gst&q=tabs+links+ajax#711a7b0dd4236190
 * @see http://code.google.com/p/jquery-hijack/
 * @param fun function to always call after hijacking (will be run in the same context as hijack())
 */
jQuery.fn.hijack = function(afterLoadFunction) {
    var target = this;

    return this
        .find('a:not(.nohijack)').click(function(event) { // attach click event to all links within (skip nohijack links)
           
           if (event.isDefaultPrevented()) // some link handler already prevented default behaviour, do nothing
               return;
              
           jQuery(target).load(this.href, function() { // load contents with AJAX 
               jQuery(this).hijack(afterLoadFunction); // and hijack egain when loaded
               if (jQuery.isFunction(afterLoadFunction)) { //  optionally run another function
                 afterLoadFunction.call(this);
               }

           });

           return false;
        })
        .end()
        .find('form:not(.nohijack)') // hijack all forms within (skip nohijack forms) 
            .hijackForm(target, afterLoadFunction)
        .end();
};

/**
 * hijackForm - used internally by hijack plugin
 * @param elem target element for form submission
 */
jQuery.fn.hijackForm = function(target, afterLoadFunction) {

    if (!jQuery.fn.ajaxForm) { // hijacking forms requires jQuery Form plugin http://malsup.com/jquery/form/
        return this;
    }

    // to skip submitting form, do something like this where you are doing validation (e.g. submit())
    // $(form_element).data('skip', true); -- this will prevent submitting the form.
    // Unfortunately, simply returning false from submit() handler is not enough - jQuery Form plugin submits it anyway
    return jQuery(this).ajaxForm({
            target: target,
            beforeSubmit: function(fdata, jqForm, options) { 
                 if (jqForm.data('skip')) { // some submit handler (validation method?) told us to stop
                    jqForm.data('skip', false); // clear
                    return false;
                }
            },
            success: function() {
                jQuery(target).hijack(afterLoadFunction);

                if (jQuery.isFunction(afterLoadFunction)) {
                    afterLoadFunction.call(target);
                }
            }
    });
};

