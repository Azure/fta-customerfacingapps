$(document).ready(function () {
    $("#query").autocomplete({
        source: function (request, response) {
            $.ajax({
                type: "GET",
                url: "/concert/suggest",
                contentType: "application/json",
                data: {
                    query: request.term
                }
            })
                .done(function (data) {
                    response(data);
                });
        },
        minLength: 3
    });
});