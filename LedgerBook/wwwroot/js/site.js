﻿// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function toggle_fnction(demoicon, demopass_type) {
    let icon = document.getElementById(demoicon);
    let pass_type = document.getElementById(demopass_type);


    icon.classList.toggle('fa-eye');
    icon.classList.toggle('fa-eye-slash');

    const type = pass_type
        .getAttribute('type') === 'password' ?
        'text' : 'password';
    pass_type.setAttribute('type', type);

}


function NumberValidation(input) {
    let value = input.value.replace(/\D/g, '');
    if (value.length > 10) {
        value = value.slice(0, 10);
    }
    input.value = value;
}

function PincodeValidation(input) {
    let value = input.value.replace(/\D/g, '');
    if (value.length > 6) {
        value = value.slice(0, 6);
    }
    input.value = value;
}

function RemoveWhiteSpace(input) {
    let value = input.value;

    value = value.replace(/\s/g, '');
    if (!value) {
        input.value = value;
    }
}

function TrimInput(input) {
    let value = input.value;
    value = value.trim();
    input.value = value;
}

function HandleResponse(response, replacePartialViewId) {
    if (response) {
        if (response.redirectUrl) {
            window.location.href = response.redirectUrl;
        } else {
            $(replacePartialViewId).html(response.html);
        }
        if (response.message) {
            if (response.errorType == 1) {
                Toaster(response.message);
            } else {
                Toaster(response.message, "error");
            }
        }
    }
}

function changePasswordModal() {
    $.ajax({
        url: "/User/RenderChangePassword",
        type: "GET",
        success: function (response) {
            $("#changepassword-body-id").html(response)
            // HandleResponse(response,"#changepassword-body-id")
        }
    })
}

$(document).on("submit", "#change-password-form", function (e) {
    e.preventDefault();
    let form = $("#change-password-form")
    if (form.valid()) {
        let formData = new FormData(this);

        $.ajax({
            url: "/User/ChangePassword",
            type: "POST",
            contentType: false,
            processData: false,
            data: formData,
            success: function (response) {
                if (response.success) {
                    Toaster(response.message);
                    $('.btn-close').click();
                } else {
                    Toaster(response.message, "error");
                }
            }
        })
    }
})

