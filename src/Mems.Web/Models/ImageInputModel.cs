/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Supplies display data to the shared image-upload partial.
 */
namespace Mems.Web.Models;
/// <summary>Contains the current image reference and label for the shared upload partial.</summary>
public record ImageInputModel(string? CurrentImage, string Label);
