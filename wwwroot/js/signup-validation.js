// Client rules for the customer signup fields that have no jQuery
// unobtrusive adapter: terms acceptance and the SystemSettings-driven
// password policy. The server re-validates both in AccountController.
(function () {
    const $ = window.jQuery;
    if (!$ || !$.validator || !$.validator.unobtrusive) return;

    $.validator.addMethod('mustbetrue', function (value, element) {
        return element.checked === true;
    });

    $.validator.addMethod('passwordpolicy', function (value, element) {
        if (!value) return true; // [Required] reports the empty field

        const form = element.form;
        const minLength = Number((form && form.dataset.passwordMinLength) || 8);
        if (value.length < minLength) return false;

        const requireStrong = !(form && form.dataset.requireStrongPassword === 'false');
        if (!requireStrong) return true;

        return /[A-Z]/.test(value) &&
            /[a-z]/.test(value) &&
            /\d/.test(value) &&
            /[^A-Za-z0-9]/.test(value);
    });

    $.validator.unobtrusive.adapters.addBool('mustbetrue');
    $.validator.unobtrusive.adapters.addBool('passwordpolicy');
})();
