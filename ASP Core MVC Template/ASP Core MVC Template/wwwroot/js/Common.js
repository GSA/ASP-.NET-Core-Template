$(document).ready(function () {
    $(".modal-header").on("mousedown", function (mousedownEvt) {
        var $draggable = $(this);
        var x = mousedownEvt.pageX - $draggable.offset().left,
            y = mousedownEvt.pageY - $draggable.offset().top;
        $("body").on("mousemove.draggable", function (mousemoveEvt) {
            $draggable.closest(".modal-dialog").offset({
                "left": mousemoveEvt.pageX - x,
                "top": mousemoveEvt.pageY - y
            });
        });
        $("body").one("mouseup", function () {
            $("body").off("mousemove.draggable");
        });
        $draggable.closest(".modal").one("bs.modal.hide", function () {
            $("body").off("mousemove.draggable");
        });
    });
});

function ConfigureKendoGridPaginationControlsFor508(gridId) {
    var grid = $('#' + gridId).data('kendoGrid');
    if (grid == undefined || grid == null)
        return;

    ConfigureFirstPageControl(gridId);
    ConfigureLastPageControl(gridId);
    ConfigurePrevPageControl(gridId);
    ConfigureNextPageControl(gridId);
    ConfigurePageSize(gridId);
}

function ConfigureFirstPageControl(gridId) {
    $("#" + gridId + " .k-grid-pager .k-pager-nav span.k-i-arrow-end-left")
        .removeClass("k-icon")
        .text("First Page")
        .parent().css({
            'padding': '0 6px',
            'border': 'solid 1px #b2b2b2',
            'border-radius': '4px 0 0 4px',
            'text-decoration': 'none'
        });
}

function ConfigureLastPageControl(gridId) {
    $("#" + gridId + " .k-grid-pager .k-pager-nav span.k-i-arrow-end-right")
        .removeClass("k-icon")
        .text("Last Page")
        .parent().css({
            'padding': '0 6px',
            'border': 'solid 1px #b2b2b2',
            'border-radius': '0 4px 4px 0',
            'text-decoration': 'none'
        });
}

function ConfigurePrevPageControl(gridId) {
    $("#" + gridId + " .k-grid-pager .k-pager-nav span.k-i-arrow-60-left")
        .removeClass("k-icon")
        .text("Prev Page")
        .parent().css({
            'padding': '0 6px',
            'border': 'solid 1px #b2b2b2',
            'border-radius': '0 4px 4px 0',
            'text-decoration': 'none'
        });
}

function ConfigureNextPageControl(gridId) {

    $("#" + gridId + " .k-grid-pager .k-pager-nav span.k-i-arrow-60-right")
        .removeClass("k-icon")
        .text("Next Page")
        .parent().css({
            'padding': '0 6px',
            'border': 'solid 1px #b2b2b2',
            'border-radius': '4px 0 0 4px',
            'text-decoration': 'none'
        })
        .parent().removeClass("k-state-disabled");
}

function ConfigurePageSize(gridId) {
    $("#" + gridId + " .k-pager-sizes select").attr("id", "pageSize");
    // Adding the 'Page Size' text is wrapping the text in bottom of grid.
    //$("#" + gridId + " .k-pager-sizes").before('<label for="pageSize" style="color:#b2b2b2; font-size: 16px; padding-left: 30px; margin-right: -20px;">Page Size</label>');
}

function GSA_alert(content) {
    var alert = $("<div></div>").kendoAlert({
        title: "CAAM Web Template",
        content: content
    }).data("kendoAlert");
    
    alert.open();
    $(".k-dialog-buttongroup").removeClass("k-dialog-button-layout-stretched");

    return alert;  // so caller can subscribe to close event.
}

function GSA_confirm(content) {
    var  confirm = $("<div></div>").kendoConfirm({
        title: "CAAM Web Template",
        content: content
    }).data("kendoConfirm")

    confirm.open();

    $(".k-dialog-buttongroup").removeClass("k-dialog-button-layout-stretched");

    return confirm;
}

// This will overlay the grid-container if there is an error.
function ShowOverlay() {
    $('#overlay').css('height', $('.grid-container').css('height'));
    $('#overlay').css('width', $('.grid-container').css('width'));
    $('#overlay').css('top', $('.grid-container').position().top);
    $('#overlay').css('left', $('.grid-container').position().left);
    $('#overlay').show();
}
