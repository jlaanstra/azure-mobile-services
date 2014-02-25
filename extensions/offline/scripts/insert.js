/// <reference path="mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');

function insert(item, user, request) {

    //new items cannot have a timestamp
    if (item.__version !== undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "insert operation cannot have __version");
        return;
    }
    //item cannot set isDeleted
    if (item.isDeleted !== undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "item cannot set isDeleted, isDeleted is a reserved column name");
        return;
    }

    // a newly inserted item is not deleted
    item.isDeleted = false;

    request.execute({
        systemProperties: ['*'],
        success: function () {
            responseHelper.sendSuccessResponse(request, "", [item], [], {});
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}