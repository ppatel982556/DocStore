$(document).ready(function () {

    initializeSidebar();

    initializeProfileMenu();

    initializeNotifications();

    initializeRoleDropdown();

});

function initializeSidebar() {

    $("#sidebarToggle").on("click", function () {

        $(".sidebar").toggleClass("collapsed");

        $(".main-container").toggleClass("expanded");

    });

    $("#collapseSidebar").on("click", function () {

        $(".sidebar").toggleClass("collapsed");

        $(".main-container").toggleClass("expanded");

    });

}

function initializeProfileMenu() {

    const $menu = $("#profileMenu");

    $("#profileMenuBtn").on("click", function (e) {

        e.stopPropagation();

        $menu.fadeToggle(150);

    });

    $(document).on("click", function () {

        $menu.fadeOut(150);

    });

    $menu.on("click", function (e) {

        e.stopPropagation();

    });

}

function initializeNotifications() {

    $("#notificationBtn").on("click", function () {

        $("<div />").kendoNotification({
            autoHideAfter: 2200,
            stacking: "down"
        }).data("kendoNotification")
          .show("No new notifications right now.", "info");

    });

}

function initializeRoleDropdown() {

    if (!$("#roleDropdown").length)
        return;

    $("#roleDropdown").kendoDropDownList({
        valuePrimitive: true,

        change: function () {

            const roleId = this.value();

            if (!roleId) {
                return;
            }

            switchRole(roleId);

        }

    });

}

function switchRole(roleId) {

    $.ajax({

        url: "/Role/SwitchRole",

        type: "POST",

        data: {

            roleId: roleId

        },

        success: function () {

            location.reload();

        },

        error: function () {

            $("<div />").kendoNotification({

                autoHideAfter: 3000

            }).data("kendoNotification")
              .show("Unable to switch role. The UI is ready, but the backend endpoint is missing or unavailable.", "error");

        }

    });

}
