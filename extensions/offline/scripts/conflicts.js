var responseHelper = require('../shared/responseHelper.js');
var async = require('async');

exports.resolveConflictOnServer = function (currentItem, newItem, resolveStrategy) {
    return resolveStrategy(currentItem, newItem);
}

exports.resolveConflictOnClient = function (request, currentItem, newItem, type) {
    responseHelper.sendConflictResponse(request, currentItem.__version, currentItem, newItem, { conflictType: type });
}

exports.processResult = function (request, results, table, type, strategy) {

    if (!Array.isArray(results)) {
        results = [results];
    }

    function updateItem(item, callback) {
        delete item.__version;
        if (item.id) {
            table.update(item, {
                systemProperties: ['*'],
                success: function () {
                    callback(null, item);
                },
                error: function (err) {
                    callback(err, null);
                }
            });
        } else {
            table.insert(item, {
                systemProperties: ['*'],
                success: function () {
                    callback(null, item);
                },
                error: function (err) {
                    callback(err, null);
                }
            });
        }
    }

    function done(err, updatedResults) {
        if (err !== null) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
        else {
            var nondeleted = [];
            var deleted = [];
            updatedResults.map(function (r) {
                if (!r.isDeleted) {
                    nondeleted.push(r);
                } else {
                    deleted.push(r.id);
                }
            });
            responseHelper.sendSuccessResponse(request, "", nondeleted, deleted, { conflictResolved: strategy, conflictType: type });
        }
    }

    async.map(results, updateItem, done);
}

exports.getConflictType = function (currentItem, newItem) {
    var type = conflictType.UpdateUpdate;
    if (currentItem.isDeleted && newItem.isDeleted) {
        type = conflictType.DeleteDelete;
    }
    else if (currentItem.isDeleted && !newItem.isDeleted) {
        type = conflictType.DeleteUpdate;
    }
    else if (!currentItem.isDeleted && newItem.isDeleted) {
        type = conflictType.UpdateDelete;
    }
    return type;
}

conflictType = {
    ///<field name="UpdateUpdate" type="Number">Both the server and the local object have been changed.</field>
    UpdateUpdate: 0,
    ///<field name="UpdateDelete" type="Number">The server object has changed while the local object was deleted.</field>
    UpdateDelete: 1,
    ///<field name="DeleteUpdate" type="Number">The server object was deleted while the local object has changed.</field>
    DeleteUpdate: 2,
    ///<field name="DeleteDelete" type="Number">Both the server and the local object have been deleted.</field>
    DeleteDelete: 3,
}



// strategies

// lastest write wins strategy
exports.latestWriteWins = function (currentItem, newItem) {
    return newItem;
}

// server wins strategy
exports.serverWins = function (currentItem, newItem) {
    return currentItem;
}

// server wins strategy
exports.duplicationApply = function (currentItem, newItem) {
    delete newItem.id;
    return [currentItem, newItem];
}